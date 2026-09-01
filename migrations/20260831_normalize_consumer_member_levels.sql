-- 严格保留五档消费者会员等级：0、1、500、2000、5000。
UPDATE Crm_Customers
SET MemberLevelId = '00000000000000000000000000000001'
WHERE MemberLevelId = 'MEMBER_LEVEL_0';

UPDATE Crm_MemberLevelHistories
SET MemberLevelId = '00000000000000000000000000000001'
WHERE MemberLevelId = 'MEMBER_LEVEL_0';

UPDATE Crm_MemberLevels
SET LevelName = '基础会员', MinSpent = 0, DiscountRate = 1, PointsMultiplier = 1
WHERE MemberLevelId = '00000000000000000000000000000001';

DELETE FROM Crm_MemberLevels WHERE MemberLevelId = 'MEMBER_LEVEL_0';
