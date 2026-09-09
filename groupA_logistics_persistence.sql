-- A 组物流持久化增量迁移；Oracle 18c，先在隔离 schema 验证。
-- 使用 SQL*Plus / SQLcl 脚本模式执行。DDL 会隐式提交，不属于业务事务。
-- 不删表、不清理历史数据；重复基础发货单会阻止迁移，需要人工核对。
WHENEVER SQLERROR EXIT SQL.SQLCODE

DECLARE
    duplicate_count NUMBER;
    constraint_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO duplicate_count FROM (
        SELECT OrderID, SupplierID FROM Log_ExpressDeliveries
        GROUP BY OrderID, SupplierID HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, '基础发货单存在重复订单供应商组合，请先人工核对');
    END IF;
    SELECT COUNT(*) INTO constraint_count FROM USER_CONSTRAINTS
        WHERE TABLE_NAME = 'LOG_EXPRESSDELIVERIES' AND CONSTRAINT_NAME = 'UQ_LED_ORDER_SUPPLIER';
    IF constraint_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE Log_ExpressDeliveries ADD CONSTRAINT UQ_LED_ORDER_SUPPLIER UNIQUE (OrderID, SupplierID)';
    END IF;
END;
/

DECLARE
    table_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO table_count FROM USER_TABLES WHERE TABLE_NAME = 'LOG_LOGISTICSDETAILS';
    IF table_count = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE TABLE Log_LogisticsDetails (
                DeliveryId VARCHAR2(36) NOT NULL,
                CarrierCode VARCHAR2(30 CHAR),
                CarrierName VARCHAR2(100 CHAR),
                TrackingNo VARCHAR2(100 CHAR),
                PackageTemperature VARCHAR2(20) NOT NULL,
                EstimatedArrivalAt TIMESTAMP(7),
                CarrierTrackingKey VARCHAR2(64),
                Remark VARCHAR2(300 CHAR),
                CONSTRAINT PK_LLD PRIMARY KEY (DeliveryId),
                CONSTRAINT FK_LLD_DELIVERY FOREIGN KEY (DeliveryId) REFERENCES Log_ExpressDeliveries(DeliveryID),
                CONSTRAINT UQ_LLD_TRACKING UNIQUE (CarrierTrackingKey),
                CONSTRAINT CK_LLD_ZONE CHECK (PackageTemperature IN ('CHILLED','FROZEN','AMBIENT'))
            )
        ]';
    END IF;
END;
/

DECLARE
    table_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO table_count FROM USER_TABLES WHERE TABLE_NAME = 'LOG_LOGISTICSEVENTS';
    IF table_count = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE TABLE Log_LogisticsEvents (
                EventId VARCHAR2(36) NOT NULL,
                DeliveryId VARCHAR2(36) NOT NULL,
                RequestHash VARCHAR2(64) NOT NULL,
                SequenceNo NUMBER(10) NOT NULL,
                StatusCode VARCHAR2(30) NOT NULL,
                Location VARCHAR2(200 CHAR),
                Description VARCHAR2(500 CHAR) NOT NULL,
                OccurredAt TIMESTAMP(7) NOT NULL,
                TemperatureCelsius NUMBER,
                IsTemperatureException NUMBER(1) DEFAULT 0 NOT NULL,
                CONSTRAINT PK_LLE PRIMARY KEY (EventId),
                CONSTRAINT FK_LLE_DELIVERY FOREIGN KEY (DeliveryId) REFERENCES Log_ExpressDeliveries(DeliveryID),
                CONSTRAINT UQ_LLE_SEQUENCE UNIQUE (DeliveryId, SequenceNo),
                CONSTRAINT CK_LLE_SEQUENCE CHECK (SequenceNo >= 0),
                CONSTRAINT CK_LLE_TEMP CHECK (IsTemperatureException IN (0,1)),
                CONSTRAINT CK_LLE_STATUS CHECK (StatusCode IN (
                    'SHIPPED','IN_TRANSIT','OUT_FOR_DELIVERY','DELIVERED','EXCEPTION','RETURNING','RETURNED'))
            )
        ]';
    END IF;
END;
/

-- 执行后核对约束均为 ENABLED / VALIDATED；已有同名表仍需比对列结构，不自动覆盖。
SELECT TABLE_NAME, CONSTRAINT_NAME, STATUS, VALIDATED FROM USER_CONSTRAINTS
WHERE TABLE_NAME IN ('LOG_LOGISTICSDETAILS','LOG_LOGISTICSEVENTS')
   OR CONSTRAINT_NAME = 'UQ_LED_ORDER_SUPPLIER'
ORDER BY TABLE_NAME, CONSTRAINT_NAME;
