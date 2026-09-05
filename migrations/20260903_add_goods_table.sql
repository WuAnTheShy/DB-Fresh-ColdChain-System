-- =====================================================================
-- 20260903 新增货物表 Inv_Goods，物品表 Inv_Products 退化为纯目录
-- 货物 = (ProductID, SupplierID) 复合主码，承载售价/上下架/温区/保质期/图文。
-- 售价(SalePrice) 单独落库；supplyprice（最终供货价=团长进价）由价格规则实时计算，不落库。
-- =====================================================================

-- 1) 建货物表
CREATE TABLE Inv_Goods (
    ProductID       VARCHAR2(36)   NOT NULL,
    SupplierID      VARCHAR2(36)   NOT NULL,
    SalePrice       NUMBER(10,2)   DEFAULT 0,
    Status          VARCHAR2(20)   DEFAULT 'ACTIVE',
    StorageReq      VARCHAR2(20),
    ShelfLifeHours  NUMBER(10),
    Description     VARCHAR2(2000),
    CreateTime      DATE,
    UpdateTime      DATE,
    CONSTRAINT PK_Inv_Goods PRIMARY KEY (ProductID, SupplierID),
    CONSTRAINT FK_Inv_Goods_Product  FOREIGN KEY (ProductID)  REFERENCES Inv_Products(ProductID),
    CONSTRAINT FK_Inv_Goods_Supplier FOREIGN KEY (SupplierID) REFERENCES Inv_Suppliers(SupplierID)
);

-- 2) 数据迁移：把现有物品的 SupplierID/DefaultPrice/Status 迁到货物表
INSERT INTO Inv_Goods (ProductID, SupplierID, SalePrice, Status, StorageReq, ShelfLifeHours, Description, CreateTime, UpdateTime)
SELECT p.ProductID, p.SupplierID, p.DefaultPrice, p.Status, p.StorageReq, p.ExpiryHours, p.Description, SYSDATE, SYSDATE
FROM Inv_Products p
WHERE p.SupplierID IS NOT NULL;

-- 3) 物品表退化：移除 供应商归属/售价/上下架（温区、保质期保留为物品默认值，货物可覆盖）
ALTER TABLE Inv_Products DROP COLUMN SupplierID;
ALTER TABLE Inv_Products DROP COLUMN DefaultPrice;
ALTER TABLE Inv_Products DROP COLUMN Status;

-- 注：Inv_SupplierPrices 由 Inv_Goods 取代，可另行废弃删除。
