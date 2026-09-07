-- 已收货订单的多维度商品评价（Oracle）
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
    -- 现有兼容库以 DetailId 为主键；触发器会保持 OrderDetailId 与其一致。
    CONSTRAINT FK_Eval_OrderDetail FOREIGN KEY (OrderDetailId) REFERENCES Biz_OrderDetails(DetailId),
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
