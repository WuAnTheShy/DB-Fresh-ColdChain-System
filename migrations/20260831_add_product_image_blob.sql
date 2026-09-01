-- ============================================================
-- 商品图片改存数据库 BLOB —— A 组数据库修改脚本
-- 数据库: Oracle 18c（111.231.80.220/XE，用户 COLDCHAIN）
--
-- 背景：项目无共享文件服务器，照片若存各自本机磁盘，
--       其他同学/其他组访问时必然 404。改为 BLOB 入库后，
--       所有端统一通过 GET /images/product/{ImageID} 读取照片。
--
-- 前置：20260831_add_product_description_images.sql（已执行，
--       Inv_ProductImages 表与 Inv_Products.Description 列均已存在）。
-- 本脚本已于 2026-08-31 在库上执行，此处留档备查。
-- ============================================================

-- 1. 图片表新增 BLOB 列存照片本体 + MIME 类型列（纯新增列，不动现有数据）
ALTER TABLE INV_PRODUCTIMAGES ADD (
    ImageData BLOB,
    ImageType VARCHAR2(20)
);

COMMENT ON COLUMN INV_PRODUCTIMAGES.ImageData IS '图片二进制数据（BLOB，供应商上传，避免多机部署时文件丢失）';
COMMENT ON COLUMN INV_PRODUCTIMAGES.ImageType IS '图片 MIME 类型（如 image/webp）';

-- 2. 查询索引：所有图片查询都按 ProductID 过滤（参照 IX_PE_* 的次要索引命名风格）
CREATE INDEX IX_PRODUCTIMAGES_PRODUCT ON INV_PRODUCTIMAGES (PRODUCTID);

-- 3.（可选）清理旧的失效图片行：BLOB 方案上线前的行没有 ImageData，
--    地址指向各机器本地不存在的文件，建议由供应商在“我的报价”页删除重传，
--    或统一执行下方语句清空后重新上传：
-- DELETE FROM INV_PRODUCTIMAGES;
-- COMMIT;

COMMIT;
