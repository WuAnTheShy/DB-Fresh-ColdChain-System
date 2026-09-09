-- 商品图文改为"供应商×商品"归属 —— A 组数据库修改脚本
-- 数据库: Oracle 18c（111.231.80.220/XE，用户 COLDCHAIN）
--
-- 背景：一个商品可能由多家供应商供货，图文原先挂在商品上，
--       供应商之间会互相覆盖。改为：
--       1) 图片表加 SupplierID：供应商上传的图归自己；
--          SupplierID 为空 = 平台通用图（所有供应商可见、不可删）
--       2) 供应商简介挂到报价表 Inv_SupplierPrices（报价即供货关系），
--          未填写时兜底显示 Inv_Products.Description
--
-- 本脚本已于 2026-09-01 在库上执行，此处留档备查。

-- 1. 图片归属供应商（NULL=平台通用图）
ALTER TABLE INV_PRODUCTIMAGES ADD (
    SupplierID VARCHAR2(36)
);
COMMENT ON COLUMN INV_PRODUCTIMAGES.SupplierID IS '上传图片的供应商ID；NULL=平台通用图（所有供应商可见，供应商不可删除）';

-- 2. 供应商对该商品的文字介绍（挂在报价记录上，各家互不影响）
ALTER TABLE INV_SUPPLIERPRICES ADD (
    Description VARCHAR2(2000)
);
COMMENT ON COLUMN INV_SUPPLIERPRICES.Description IS '该供应商对该商品的文字介绍（团长可参考/复制/改写）；NULL 时兜底 Inv_Products.Description';

-- 3.（已执行）清理 6 条无 BLOB 数据的死行（土豆 3 + 云南野生菌 3），
--    待同学提供照片文件后再带供应商归属重新插入：
-- DELETE FROM INV_PRODUCTIMAGES WHERE ImageData IS NULL;
-- COMMIT;

COMMIT;
