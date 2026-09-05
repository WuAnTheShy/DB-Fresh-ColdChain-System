-- =====================================================================
-- 20260905 支持“同一商品由多个供应商供货、同一笔订单可同时购买”的配套脚本
-- 适用版本：full-version6（Oracle，表名/字段与代码模型一致）
--
-- 【说明】
-- 本次代码改造没有修改任何表结构：交易身份下沉为 (商品ID, 供应商ID) 后，
--   · 下单前库存校验 与 发货扣批次 均改为按 (ProductID, SupplierID) 读取 Inv_StockBatches；
--   · Inv_StockSummary 仍为“商品级总池”行，用于管理端展示，出库/发货时同步扣减保持守恒。
-- 因此本脚本只需执行 Part A 即可让新查询路径受益；Part B/C 是自检与可选演示数据。
-- =====================================================================

-- ---------------------------------------------------------------------
-- PART A（必执行）：为批次表增加 (ProductID, SupplierID) 联合索引，
-- 支撑供应商级 FEFO 锁定查询 GetByProductAndSupplierForUpdateAsync
-- 与供应商级可用量合计 GetActiveTotalByProductAndSupplierAsync。
-- 重复执行会被静默忽略。
-- ---------------------------------------------------------------------
BEGIN
    EXECUTE IMMEDIATE
        'CREATE INDEX IX_StockBatches_Product_Supplier ON Inv_StockBatches (ProductID, SupplierID)';
EXCEPTION
    WHEN OTHERS THEN
        NULL; -- ORA-01408：索引已存在，忽略
END;
/

-- ---------------------------------------------------------------------
-- PART B（自检，只读，可随时执行）：
-- 同一商品多个供应商的批次合计，应等于商品级汇总行数量（守恒）。
-- 若下方查询返回行，说明该商品的历史出入库存在汇总与批次不一致，
-- 可由“出库时汇总自动修正”或重新入库校正；不影响下单正确性（下单读批次）。
-- ---------------------------------------------------------------------
SELECT s.ProductID,
       SUM(s.TotalQty)   AS SummaryTotal,
       NVL(b.BatchTotal, 0) AS BatchTotal,
       SUM(s.TotalQty) - NVL(b.BatchTotal, 0) AS DiffQty
FROM Inv_StockSummary s
LEFT JOIN (
    SELECT ProductID, SUM(CurrentQty) AS BatchTotal
    FROM Inv_StockBatches
    WHERE Status = 'ACTIVE'
      AND (ExpiryDate IS NULL OR ExpiryDate >= SYSDATE)
    GROUP BY ProductID
) b ON b.ProductID = s.ProductID
GROUP BY s.ProductID, b.BatchTotal
HAVING SUM(s.TotalQty) <> NVL(b.BatchTotal, 0);

-- 查看当前“同一商品已被多个供应商供货”的情况（应为业务演示的目标数据）
SELECT ProductID, COUNT(DISTINCT SupplierID) AS SupplierCount
FROM Inv_Goods
WHERE Status = 'ACTIVE'
GROUP BY ProductID
HAVING COUNT(DISTINCT SupplierID) > 1;

-- ---------------------------------------------------------------------
-- PART C（可选，演示数据模板）：把同一商品挂到第二个供应商名下并各自建库存。
-- 执行前请先手工把下面的 :xxx 替换成真实 ID（商品、新供应商），
-- 并通过“供应商后台 → 报价 → 货物上架 → 入库批次”界面完成，效果相同。
-- ---------------------------------------------------------------------
-- 1) 为第二个供应商建立货物（售价请自定，Status 必须为 ACTIVE 才会进入可售目录）
-- INSERT INTO Inv_Goods (ProductID, SupplierID, SalePrice, Status, StorageReq, ShelfLifeHours, Description, CreateTime, UpdateTime)
-- VALUES ('P-商品ID', 'S-第二个供应商ID', 12.50, 'ACTIVE', 'REFRIGERATED', 48, NULL, SYSDATE, SYSDATE);

-- 2) 为该 (商品, 供应商) 建立库存批次（CurrentQty 即可售量）
-- INSERT INTO Inv_StockBatches (BatchID, ProductID, SupplierID, BatchNo, ProductionDate, ExpiryDate, InPrice, InitialQty, CurrentQty, Status)
-- VALUES (SYS_GUID(), 'P-商品ID', 'S-第二个供应商ID', 'B20260905002', SYSDATE, SYSDATE + 20, 8.00, 100, 100, 'ACTIVE');

-- 3) 同步累加商品级汇总（若该商品已有汇总行则改为 UPDATE 累加）
-- INSERT INTO Inv_StockSummary (StockID, ProductID, TotalQty, LockedQty, AvailableQty, UpdateTime)
-- VALUES (SYS_GUID(), 'P-商品ID', 100, 0, 100, SYSDATE);

-- 4) 让某团长把 (商品, 第二个供应商) 加入其可售货架（CRM_PRODUCT_ENTRIES，
--    通过“团长后台 → 上架商品/供应商报价”界面操作，勿手工插该表，避免漏同步图片/描述字段）。

-- ---------------------------------------------------------------------
-- PART D（验收，只读）：验证“一笔订单里两个供应商明细各自成立”
-- 下单后执行（订单头/明细已入库）：
-- SELECT OrderID, ProductID, SupplierID, Quantity, UnitPrice
-- FROM Biz_OrderDetails
-- WHERE OrderID = '订单ID';          -- 应看到同一 ProductID 的两行，SupplierID 分别为两家
-- SELECT OrderID, SupplierID, SUM(Quantity) AS Qty
-- FROM Biz_FulfillmentItems           -- 以实际履约表名为准
-- GROUP BY OrderID, SupplierID;       -- 应看到按供应商拆出的两批发货
