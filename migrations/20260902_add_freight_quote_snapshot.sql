-- B 组：为订单增加下单时运费报价快照。
-- 仅修改 B 组负责的 Biz_Orders，不触碰 A 组运费模板或物流表。

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'BIZ_ORDERS'
      AND COLUMN_NAME = 'FREIGHTQUOTESNAPSHOT';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE Biz_Orders ADD FreightQuoteSnapshot CLOB';
    END IF;
END;
/

COMMENT ON COLUMN Biz_Orders.FreightQuoteSnapshot IS
    'A组运费报价结果JSON快照，包含目的地、规则摘要、商品计费项和计算时间';

COMMIT;
