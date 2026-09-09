-- Group B (C端交易与营销组) 建表脚本
-- 数据库: Oracle 18c
-- 注意: 请先执行项目设计文档中完整的建表脚本后再按需创建

-- 1. Crm_MemberLevels - 会员等级定义
CREATE TABLE Crm_MemberLevels (
    MemberLevelId   VARCHAR2(36)   PRIMARY KEY,
    LevelName       VARCHAR2(50)   NOT NULL,    -- 普通/银卡/金卡/钻石
    MinSpent        NUMBER(10,2)   NOT NULL,    -- 该等级最低消费门槛
    DiscountRate    NUMBER(4,3)    DEFAULT 1,   -- 折扣率 0.95=95折
    PointsMultiplier NUMBER         DEFAULT 1,   -- 积分倍率
    CONSTRAINT UQ_MemberLevel_MinSpent UNIQUE (MinSpent),
    CONSTRAINT CK_MemberLevel_MinSpent CHECK (MinSpent >= 0),
    CONSTRAINT CK_MemberLevel_Discount CHECK (DiscountRate > 0 AND DiscountRate <= 1),
    CONSTRAINT CK_MemberLevel_Points CHECK (PointsMultiplier >= 1)
);

-- 2. Crm_Customers - 消费者
CREATE TABLE Crm_Customers (
    CustomerId      VARCHAR2(36)   PRIMARY KEY,
    OpenId          VARCHAR2(100),
    CustomerName    VARCHAR2(100)  NOT NULL,
    Phone           VARCHAR2(20)   NOT NULL,
    Email           VARCHAR2(100),
    Avatar          VARCHAR2(50),                -- 预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn）
    PasswordHash    VARCHAR2(255)  NOT NULL,
    PromoterId      VARCHAR2(36),                 -- 所属团长ID（C组字符串GUID）
    MemberLevelId   VARCHAR2(36),
    TotalSpent      NUMBER(10,2)   DEFAULT 0,     -- 累计消费
    Points          NUMBER         DEFAULT 0,     -- 当前积分
    GrowthValue     NUMBER         DEFAULT 0,
    BindExpireTime  DATE,
    CreatedAt       DATE           DEFAULT SYSDATE,
    UpdatedAt       DATE,
    CONSTRAINT UQ_Customer_Phone UNIQUE (Phone),
    CONSTRAINT UQ_Customer_OpenId UNIQUE (OpenId),
    CONSTRAINT FK_Customer_Level    FOREIGN KEY (MemberLevelId) REFERENCES Crm_MemberLevels(MemberLevelId)
);

-- 3. Crm_UserAddresses - 收货地址
CREATE TABLE Crm_UserAddresses (
    AddressId       VARCHAR2(36)   PRIMARY KEY,
    CustomerId      VARCHAR2(36)   NOT NULL,
    ReceiverName    VARCHAR2(50)   NOT NULL,
    Phone           VARCHAR2(20)   NOT NULL,
    Province        VARCHAR2(50),
    City            VARCHAR2(50),
    District        VARCHAR2(50),
    DetailAddress   VARCHAR2(200),
    IsDefault       NUMBER(1)      DEFAULT 0,
    CreatedAt       DATE           DEFAULT SYSDATE,
    CONSTRAINT FK_Address_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT CK_Address_IsDefault CHECK (IsDefault IN (0, 1))
);

-- 同一消费者最多只能有一条 IsDefault=1 的地址；非默认地址不参与唯一约束。
CREATE UNIQUE INDEX UQ_Address_OneDefault
    ON Crm_UserAddresses (
        CASE WHEN IsDefault = 1 THEN CustomerId END
    );

CREATE INDEX IX_Address_Customer
    ON Crm_UserAddresses (CustomerId, IsDefault);

-- 4. Mkt_Coupons - 优惠券模板
CREATE TABLE Mkt_Coupons (
    CouponId         VARCHAR2(36)   PRIMARY KEY,
    CouponName       VARCHAR2(100)  NOT NULL,
    CouponType       VARCHAR2(20)   DEFAULT 'NORMAL' NOT NULL, -- NORMAL/SPECIAL
    MinOrderAmount   NUMBER(10,2)   DEFAULT 0,
    DiscountAmount   NUMBER(10,2)   NOT NULL,
    TotalQuantity    NUMBER         NOT NULL,
    RemainingQuantity NUMBER        NOT NULL,
    StartTime        DATE           NOT NULL,
    EndTime          DATE           NOT NULL,
    Status           NUMBER(1)      DEFAULT 1,   -- 0=停用 1=启用
    CONSTRAINT CK_Coupon_Amount CHECK (
        MinOrderAmount >= 0 AND DiscountAmount > 0
    ),
    CONSTRAINT CK_Coupon_Quantity CHECK (
        TotalQuantity >= 0
        AND RemainingQuantity >= 0
        AND RemainingQuantity <= TotalQuantity
    ),
    CONSTRAINT CK_Coupon_Time CHECK (EndTime > StartTime),
    CONSTRAINT CK_Coupon_Status CHECK (Status IN (0, 1)),
    CONSTRAINT CK_Coupon_Type CHECK (CouponType IN ('NORMAL', 'SPECIAL'))
);

-- 5. Mkt_CouponRecords - 用户领券/用券记录
CREATE TABLE Mkt_CouponRecords (
    RecordId        VARCHAR2(36)   PRIMARY KEY,
    CouponId        VARCHAR2(36)   NOT NULL,
    CustomerId      VARCHAR2(36)   NOT NULL,
    OrderId         VARCHAR2(36),                 -- 核销订单
    Status          NUMBER(1)      DEFAULT 0,     -- 0=未用 1=已用 2=过期
    UsedAt          DATE,
    CreatedAt       DATE           DEFAULT SYSDATE,
    CONSTRAINT FK_CouponRec_Coupon   FOREIGN KEY (CouponId) REFERENCES Mkt_Coupons(CouponId),
    CONSTRAINT FK_CouponRec_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT UQ_CouponRec_Customer UNIQUE (CouponId, CustomerId),
    CONSTRAINT CK_CouponRec_Status CHECK (Status IN (0, 1, 2))
);

CREATE INDEX IX_CouponRec_CustomerStatus
    ON Mkt_CouponRecords (CustomerId, Status);

-- 6. Crm_PointLogs - 积分流水 (每笔必记)
CREATE TABLE Crm_PointLogs (
    PointLogId      VARCHAR2(36)   PRIMARY KEY,
    CustomerId      VARCHAR2(36)   NOT NULL,
    ChangeAmount    NUMBER         NOT NULL,      -- 正=获得 负=扣减
    BalanceAfter    NUMBER         NOT NULL,
    ChangeType      VARCHAR2(30)   NOT NULL,      -- ORDER_EARN/REFUND_DEDUCT
    OrderId         VARCHAR2(36),
    CreatedAt       DATE           DEFAULT SYSDATE,
    CONSTRAINT FK_PointLog_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId)
);

CREATE INDEX IX_PointLog_CustomerOrderType
    ON Crm_PointLogs (CustomerId, OrderId, ChangeType);

-- B 组扩展表. Crm_MemberLevelHistories - 消费者每月定级历史
CREATE TABLE Crm_MemberLevelHistories (
    HistoryId        VARCHAR2(36) PRIMARY KEY,
    CustomerId       VARCHAR2(36) NOT NULL,
    MemberLevelId    VARCHAR2(36) NOT NULL,
    QualifiedSpent   NUMBER(12,2) NOT NULL,
    SettlementMonth  DATE NOT NULL,
    CreatedAt        DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_MLH_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT FK_MLH_Level FOREIGN KEY (MemberLevelId) REFERENCES Crm_MemberLevels(MemberLevelId),
    CONSTRAINT UQ_MLH_CustomerMonth UNIQUE (CustomerId, SettlementMonth),
    CONSTRAINT CK_MLH_QualifiedSpent CHECK (QualifiedSpent >= 0)
);

CREATE INDEX IX_MLH_CustomerMonth
    ON Crm_MemberLevelHistories (CustomerId, SettlementMonth DESC);

-- 7. Biz_Orders - 订单主表
CREATE TABLE Biz_Orders (
    OrderId         VARCHAR2(36)   PRIMARY KEY,
    OrderNo         VARCHAR2(50)   NOT NULL UNIQUE,
    CustomerId      VARCHAR2(36)   NOT NULL,
    CheckoutBatchId VARCHAR2(36),                 -- 同一次结算按团长拆单的批次ID
    PromoterId      VARCHAR2(36),
    AddressId       VARCHAR2(36)   NOT NULL,
    ReceiverName    VARCHAR2(50)   NOT NULL,      -- 下单时收件人快照
    ReceiverPhone   VARCHAR2(20)   NOT NULL,      -- 下单时电话快照
    ShippingAddress VARCHAR2(500)  NOT NULL,      -- 下单时完整地址快照
    TotalAmount     NUMBER(10,2)   NOT NULL,
    DiscountAmount  NUMBER(10,2)   DEFAULT 0,
    FreightAmount   NUMBER(10,2)   DEFAULT 0,
    FreightQuoteSnapshot CLOB,                    -- A组报价结果JSON快照，供审计与展示
    FinalAmount     NUMBER(10,2)   NOT NULL,
    CommBaseAmount  NUMBER(10,2),
    CommBonusAmount NUMBER(10,2),
    CommSettlementDate DATE,
    PointsEarned    NUMBER         DEFAULT 0,
    PointsUsed      NUMBER         DEFAULT 0 NOT NULL,
    PointsDiscountAmount NUMBER(10,2) DEFAULT 0 NOT NULL,
    OrderStatus     VARCHAR2(20)   DEFAULT 'PENDING_PAYMENT',
    PaymentExpiresAt DATE,                         -- 模拟支付截止时间
    CreatedAt       DATE           DEFAULT SYSDATE,
    UpdatedAt       DATE,
    CONSTRAINT FK_Order_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT FK_Order_Address  FOREIGN KEY (AddressId)  REFERENCES Crm_UserAddresses(AddressId),
    CONSTRAINT CK_Order_Status CHECK (OrderStatus IN (
        'PENDING_PAYMENT', 'PAID', 'SHIPPED', 'COMPLETED',
        'CANCELLED', 'REFUNDING', 'REFUNDED'
    )),
    CONSTRAINT CK_Order_Amounts CHECK (
        TotalAmount >= 0
        AND DiscountAmount >= 0
        AND FreightAmount >= 0
        AND FinalAmount >= 0
    )
);

CREATE INDEX IX_Order_CustomerCreated
    ON Biz_Orders (CustomerId, CreatedAt);

CREATE INDEX IX_Order_StatusCreated
    ON Biz_Orders (OrderStatus, CreatedAt);

CREATE INDEX IX_Order_CheckoutBatch
    ON Biz_Orders (CheckoutBatchId, CustomerId);

-- 8. Biz_OrderDetails - 订单明细
CREATE TABLE Biz_OrderDetails (
    OrderDetailId   VARCHAR2(36)   PRIMARY KEY,
    OrderId         VARCHAR2(36)   NOT NULL,
    ProductId       VARCHAR2(36)   NOT NULL,
    ProductName     VARCHAR2(200)  NOT NULL,
    Quantity        NUMBER         NOT NULL,
    UnitPrice       NUMBER(10,2)   NOT NULL,
    SubTotal        NUMBER(10,2)   NOT NULL,
    SupplierId      VARCHAR2(36),
    ReceiptStatus   VARCHAR2(20)   DEFAULT 'PENDING' NOT NULL,
    ReceivedAt      DATE,
    CONSTRAINT FK_Detail_Order FOREIGN KEY (OrderId) REFERENCES Biz_Orders(OrderId)
);

CREATE INDEX IX_OrderDetail_OrderReceipt
    ON Biz_OrderDetails (OrderId, ReceiptStatus);

-- B 组扩展表. Biz_ProductEvaluations - 已收货订单的多维度评价
CREATE TABLE Biz_ProductEvaluations (
    EvaluationId      VARCHAR2(36) PRIMARY KEY,
    OrderDetailId     VARCHAR2(36) NOT NULL,
    OrderId           VARCHAR2(36) NOT NULL,
    ProductId         VARCHAR2(36) NOT NULL,
    PromoterId        VARCHAR2(36) NOT NULL,
    CustomerId        VARCHAR2(36) NOT NULL,
    HighQuality       NUMBER(1) DEFAULT 0 NOT NULL,
    FastShipping      NUMBER(1) DEFAULT 0 NOT NULL,
    GoodPackaging     NUMBER(1) DEFAULT 0 NOT NULL,
    CostEffective     NUMBER(1) DEFAULT 0 NOT NULL,
    Affordable        NUMBER(1) DEFAULT 0 NOT NULL,
    ReliablePromoter  NUMBER(1) DEFAULT 0 NOT NULL,
    CreatedAt         DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT UQ_Eval_OrderDetail UNIQUE (OrderDetailId),
    CONSTRAINT FK_Eval_OrderDetail FOREIGN KEY (OrderDetailId) REFERENCES Biz_OrderDetails(OrderDetailId),
    CONSTRAINT FK_Eval_Order FOREIGN KEY (OrderId) REFERENCES Biz_Orders(OrderId),
    CONSTRAINT FK_Eval_Customer FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT CK_Eval_Flags CHECK (
        HighQuality IN (0, 1) AND FastShipping IN (0, 1)
        AND GoodPackaging IN (0, 1) AND CostEffective IN (0, 1)
        AND Affordable IN (0, 1) AND ReliablePromoter IN (0, 1)
    ),
    CONSTRAINT CK_Eval_Selected CHECK (
        HighQuality + FastShipping + GoodPackaging + CostEffective + Affordable + ReliablePromoter >= 1
    )
);

CREATE INDEX IX_Eval_Promoter
    ON Biz_ProductEvaluations (PromoterId, CreatedAt DESC);

-- 订单表创建后补充两条可选订单关联外键。
ALTER TABLE Mkt_CouponRecords ADD CONSTRAINT FK_CouponRec_Order
    FOREIGN KEY (OrderId) REFERENCES Biz_Orders(OrderId);

ALTER TABLE Crm_PointLogs ADD CONSTRAINT FK_PointLog_Order
    FOREIGN KEY (OrderId) REFERENCES Biz_Orders(OrderId);

-- B 组最小可联调演示数据（覆盖 8 张核心表和 1 张定级历史扩展表）
-- 演示消费者：13800138000 / FreshB2026!

INSERT INTO Crm_MemberLevels (
    MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier)
VALUES ('00000000000000000000000000000001', '普通会员', 0, 1, 1);

INSERT INTO Crm_MemberLevels (
    MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier)
VALUES ('MEMBER_LEVEL_1', '白银贵宾', 1, 1, 1);

INSERT INTO Crm_MemberLevels (
    MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier)
VALUES ('MEMBER_LEVEL_500', '黄金贵宾', 500, 1, 1);

INSERT INTO Crm_MemberLevels (
    MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier)
VALUES ('MEMBER_LEVEL_2000', '铂金贵宾', 2000, 1, 1);

INSERT INTO Crm_MemberLevels (
    MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier)
VALUES ('MEMBER_LEVEL_5000', '钻石贵宾', 5000, 1, 1);

INSERT INTO Crm_Customers (
    CustomerId, CustomerName, Phone, Email, PasswordHash, MemberLevelId,
    TotalSpent, Points, GrowthValue, CreatedAt)
VALUES (
    '10000000000000000000000000000001', 'B组演示消费者', '13800138000',
    'groupb-demo@example.com',
    'AQAAAAIAAYagAAAAELZ+JUYKNB5uhEWheNPT8V/P2ZU/5gCHQxkXY/P1OOizxu3qpbZoMEccZ7Pgs+EwKg==',
    'MEMBER_LEVEL_1', 160, 16, 0, SYSDATE);

INSERT INTO Crm_MemberLevelHistories (
    HistoryId, CustomerId, MemberLevelId, QualifiedSpent, SettlementMonth,
    CreatedAt)
VALUES (
    '80000000000000000000000000000001',
    '10000000000000000000000000000001',
    'MEMBER_LEVEL_1', 160, TRUNC(SYSDATE, 'MM'), SYSDATE);

INSERT INTO Crm_UserAddresses (
    AddressId, CustomerId, ReceiverName, Phone, Province, City, District,
    DetailAddress, IsDefault, CreatedAt)
VALUES (
    '20000000000000000000000000000001',
    '10000000000000000000000000000001',
    '演示收件人', '13800138000', '浙江省', '杭州市', '西湖区',
    '文三路演示园区1号', 1, SYSDATE);

INSERT INTO Mkt_Coupons (
    CouponId, CouponName, CouponType, MinOrderAmount, DiscountAmount, TotalQuantity,
    RemainingQuantity, StartTime, EndTime, Status)
VALUES (
    '30000000000000000000000000000001', '新人满100减20', 'NORMAL', 100, 20,
    100, 99, SYSDATE - 1, SYSDATE + 30, 1);

INSERT INTO Biz_Orders (
    OrderId, OrderNo, CustomerId, AddressId, ReceiverName, ReceiverPhone,
    ShippingAddress, TotalAmount, DiscountAmount, FreightAmount, FinalAmount,
    PointsEarned, OrderStatus, CreatedAt)
VALUES (
    '50000000000000000000000000000001', 'ORD-DEMO-B-0001',
    '10000000000000000000000000000001',
    '20000000000000000000000000000001',
    '演示收件人', '13800138000', '浙江省 杭州市 西湖区 文三路演示园区1号',
    180, 20, 0, 160, 16, 'COMPLETED', SYSDATE);

INSERT INTO Biz_OrderDetails (
    OrderDetailId, OrderId, ProductId, ProductName, Quantity, UnitPrice,
    SubTotal, SupplierId)
VALUES (
    '60000000000000000000000000000001',
    '50000000000000000000000000000001',
    'P1', '演示车厘子', 2, 90, 180, 'SUP1');

INSERT INTO Mkt_CouponRecords (
    RecordId, CouponId, CustomerId, OrderId, Status, UsedAt, CreatedAt)
VALUES (
    '40000000000000000000000000000001',
    '30000000000000000000000000000001',
    '10000000000000000000000000000001',
    '50000000000000000000000000000001', 1, SYSDATE, SYSDATE);

INSERT INTO Crm_PointLogs (
    PointLogId, CustomerId, ChangeAmount, BalanceAfter, ChangeType, OrderId,
    CreatedAt)
VALUES (
    '70000000000000000000000000000001',
    '10000000000000000000000000000001',
    16, 16, 'ORDER_EARN', '50000000000000000000000000000001',
    SYSDATE);

COMMIT;
