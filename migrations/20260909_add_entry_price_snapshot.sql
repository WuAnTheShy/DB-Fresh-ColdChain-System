-- 20260909 CRM_PRODUCT_ENTRIES 增加「动态报价快照」列
-- 背景：团长端上架商品的供应商报价由“货物静态售价”改为“供应商动态定价”
--      （A 组规则引擎：商品×供应商×数量×当前时间，见 Services/PricingService.cs）。
--      入团/上架成功时，把那一刻算出的动态报价与推荐价快照写入本表：
--        SupplyPrice   = 动态定价最终报价（团长进价）
--        DefaultPrice  = 推荐价 = SupplyPrice × 1.2（1.2 倍率不变）
--      之后团长端列表/详情与消费者端均读取该快照，不再随货物售价/价格规则实时漂移，
--      便于离线存档查询。
-- 说明：价格规则引擎逻辑无法在 SQL 中复算，历史已入团数据按下述公式以“静态基准
--       （货物售价）×1.2”回填；如需旧记录携带当时规则折扣，重新入团即可再次快照。

-- 1) 加列
ALTER TABLE CRM_PRODUCT_ENTRIES ADD (
    SupplyPrice   NUMBER(10,2),
    DefaultPrice  NUMBER(10,2)
);
COMMENT ON COLUMN CRM_PRODUCT_ENTRIES.SupplyPrice
    IS '动态报价快照：入团时刻按供应商动态定价(规则引擎)计算的最终报价/团长进价';
COMMENT ON COLUMN CRM_PRODUCT_ENTRIES.DefaultPrice
    IS '推荐价快照：入团时刻动态报价 × 1.2（倍率与历史一致）';

-- 2) 历史数据回填：取该供应商对该商品的货物售价（未命中规则的静态基准）×1.2
UPDATE CRM_PRODUCT_ENTRIES e
   SET e.SupplyPrice  = COALESCE((SELECT g.SalePrice
                                    FROM Inv_Goods g
                                   WHERE g.ProductID  = e.ProductID
                                     AND g.SupplierID = e.SupplierID), 0),
       e.DefaultPrice = ROUND(
                            COALESCE((SELECT g.SalePrice
                                        FROM Inv_Goods g
                                       WHERE g.ProductID  = e.ProductID
                                         AND g.SupplierID = e.SupplierID), 0) * 1.2, 2)
 WHERE e.SupplyPrice  IS NULL
    OR e.DefaultPrice IS NULL;

COMMIT;
