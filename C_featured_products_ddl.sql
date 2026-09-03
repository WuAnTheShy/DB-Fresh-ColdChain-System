-- ============================================================
-- 团长带货商品功能 —— 数据库修改脚本
-- 数据库: Oracle 18c
-- 说明:
--   1) 新建商品图片表 Inv_ProductImages（商品-多图，一个商品对应多张图片）
--   2) Inv_Products 新增商品文字介绍列 Description（供应商维护）
--   3) CRM_PRODUCT_ENTRIES 新增团长带货介绍列 PromoterDesc（团长文字，默认复制供应商文字）
-- ============================================================

-- 1. 新增商品图片表：一个商品对应多张图片（A 组记录商品与图片对应关系）
CREATE TABLE Inv_ProductImages (
    ImageID     VARCHAR2(36)   NOT NULL,   -- 图片ID（主键）
    ProductID   VARCHAR2(36)   NOT NULL,   -- 商品ID（关联 Inv_Products.ProductID）
    ImageUrl    VARCHAR2(500)  NOT NULL,   -- 图片地址
    SortOrder   NUMBER(5)      DEFAULT 0 NOT NULL,  -- 展示顺序（升序，取前3张展示）
    CreateTime  DATE           DEFAULT SYSDATE NOT NULL,
    CONSTRAINT PK_Inv_ProductImages PRIMARY KEY (ImageID)
);

COMMENT ON TABLE  Inv_ProductImages IS '商品图片表：一个商品对应多张图片';
COMMENT ON COLUMN Inv_ProductImages.ImageID    IS '图片ID（主键）';
COMMENT ON COLUMN Inv_ProductImages.ProductID  IS '商品ID（关联 Inv_Products.ProductID）';
COMMENT ON COLUMN Inv_ProductImages.ImageUrl   IS '图片地址';
COMMENT ON COLUMN Inv_ProductImages.SortOrder  IS '展示顺序（升序，对外取前3张）';
COMMENT ON COLUMN Inv_ProductImages.CreateTime IS '创建时间';

-- 2. Inv_Products 新增商品文字介绍列（供应商填写，供团长参考/复制/改写）
ALTER TABLE Inv_Products ADD (
    Description VARCHAR2(2000)
);
COMMENT ON COLUMN Inv_Products.Description IS '商品文字介绍（供应商维护，团长可参考或重写）';

-- 3. CRM_PRODUCT_ENTRIES 新增团长带货介绍列（团长文字，入团时默认复制供应商文字）
ALTER TABLE CRM_PRODUCT_ENTRIES ADD (
    PromoterDesc VARCHAR2(2000)
);
COMMENT ON COLUMN CRM_PRODUCT_ENTRIES.PromoterDesc
    IS '团长带货介绍文字（给消费者端展示；入团时默认复制 Inv_Products.Description，团长可修改/重写）';

-- 4.（可选）演示数据：给现有商品初始化商品文字介绍
--    商品图片由供应商登录门户（供应商“我的报价”页）上传，
--    文件保存到 wwwroot/images/products/，数据库登记到 Inv_ProductImages。
UPDATE Inv_Products
   SET Description = '【' || ProductName || '】产地直发，新鲜采摘，冷链配送，保障品质。'
 WHERE Description IS NULL;

COMMIT;

-- 5.（可选）演示数据：将已入团商品（CRM_PRODUCT_ENTRIES）的团长介绍初始化为供应商文字
UPDATE CRM_PRODUCT_ENTRIES e
   SET PromoterDesc = (SELECT p.Description FROM Inv_Products p WHERE p.ProductID = e.ProductID)
 WHERE PromoterDesc IS NULL
   AND EXISTS (SELECT 1 FROM Inv_Products p WHERE p.ProductID = e.ProductID);

COMMIT;
