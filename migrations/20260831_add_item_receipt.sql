-- 消费者按订单明细分别确认收货。
ALTER TABLE Biz_OrderDetails ADD (
    ReceiptStatus VARCHAR2(20) DEFAULT 'PENDING' NOT NULL,
    ReceivedAt DATE
);

CREATE INDEX IX_OrderDetail_Receipt
    ON Biz_OrderDetails (OrderId, ReceiptStatus);
