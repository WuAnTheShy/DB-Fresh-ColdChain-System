-- 消费者结算积分抵扣与支付后积分入账。
ALTER TABLE Biz_Orders ADD (
    PointsUsed NUMBER DEFAULT 0 NOT NULL,
    PointsDiscountAmount NUMBER(10,2) DEFAULT 0 NOT NULL
);
