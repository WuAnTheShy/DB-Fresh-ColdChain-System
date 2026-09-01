-- 消费者会员：每月定级历史（每个消费者每月仅一条）。
CREATE TABLE Crm_MemberLevelHistories (
    HistoryId        VARCHAR2(36) PRIMARY KEY,
    CustomerId       VARCHAR2(36) NOT NULL,
    MemberLevelId    VARCHAR2(36) NOT NULL,
    QualifiedSpent   NUMBER(12,2) NOT NULL,
    SettlementMonth  DATE NOT NULL,
    CreatedAt        DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_MLH_CUSTOMER FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT FK_MLH_LEVEL FOREIGN KEY (MemberLevelId) REFERENCES Crm_MemberLevels(MemberLevelId),
    CONSTRAINT CK_MLH_SPENT CHECK (QualifiedSpent >= 0),
    CONSTRAINT UQ_MLH_CUSTOMER_MONTH UNIQUE (CustomerId, SettlementMonth)
);

CREATE INDEX IX_MLH_CUSTOMER_MONTH ON Crm_MemberLevelHistories (CustomerId, SettlementMonth DESC);

-- 五档消费者等级：0、1、500、2000、5000 元。
MERGE INTO Crm_MemberLevels l USING (SELECT '00000000000000000000000000000001' id, '普通会员' name, 0 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN MATCHED THEN UPDATE SET l.LevelName=s.name, l.MinSpent=s.spent, l.DiscountRate=1, l.PointsMultiplier=1
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_1' id, '白银贵宾' name, 1 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_500' id, '黄金贵宾' name, 500 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_2000' id, '铂金贵宾' name, 2000 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_5000' id, '钻石贵宾' name, 5000 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
