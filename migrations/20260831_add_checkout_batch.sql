-- 消费者跨团长结算：为拆分后的子订单增加共同批次与支付截止时间。
ALTER TABLE Biz_Orders ADD (
    CheckoutBatchId VARCHAR2(36),
    PaymentExpiresAt DATE
);

CREATE INDEX IX_Order_CheckoutBatch
    ON Biz_Orders (CheckoutBatchId, CustomerId);
