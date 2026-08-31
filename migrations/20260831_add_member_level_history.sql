-- 消费者会员：每月定级历史（每个消费者每月仅一条）。
CREATE TABLE Crm_MemberLevelHistories (
    HistoryId        VARCHAR2(36) PRIMARY KEY,
    CustomerId       VARCHAR2(36) NOT NULL,
    MemberLevelId    VARCHAR2(36) NOT NULL,
    QualifiedSpent   NUMBER(12,2) NOT NULL,
    SettlementMonth  DATE NOT NULL,
    CreatedAt        DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_MLH_CUSTOMER FOREIGN KEY (CustomerId) REFERENCES Crm_Customers(CustomerId),
    CONSTRAINT UQ_MLH_CUSTOMER_MONTH UNIQUE (CustomerId, SettlementMonth)
);

CREATE INDEX IX_MLH_CUSTOMER_MONTH ON Crm_MemberLevelHistories (CustomerId, SettlementMonth DESC);

-- 五档消费者等级：0、1、500、2000、5000 元。
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_0' id, '基础会员' name, 0 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_1' id, '青铜会员' name, 1 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_500' id, '白银会员' name, 500 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_2000' id, '黄金会员' name, 2000 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
MERGE INTO Crm_MemberLevels l USING (SELECT 'MEMBER_LEVEL_5000' id, '钻石会员' name, 5000 spent FROM dual) s ON (l.MemberLevelId=s.id)
WHEN NOT MATCHED THEN INSERT (MemberLevelId, LevelName, MinSpent, DiscountRate, PointsMultiplier) VALUES (s.id,s.name,s.spent,1,1);
