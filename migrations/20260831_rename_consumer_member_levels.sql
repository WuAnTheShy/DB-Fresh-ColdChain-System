-- 消费者会员等级名称调整，门槛保持 0、1、500、2000、5000 不变。
UPDATE Crm_MemberLevels SET LevelName = '普通会员' WHERE MemberLevelId = '00000000000000000000000000000001';
UPDATE Crm_MemberLevels SET LevelName = '白银贵宾' WHERE MemberLevelId = 'MEMBER_LEVEL_1';
UPDATE Crm_MemberLevels SET LevelName = '黄金贵宾' WHERE MemberLevelId = 'MEMBER_LEVEL_500';
UPDATE Crm_MemberLevels SET LevelName = '铂金贵宾' WHERE MemberLevelId = 'MEMBER_LEVEL_2000';
UPDATE Crm_MemberLevels SET LevelName = '钻石贵宾' WHERE MemberLevelId = 'MEMBER_LEVEL_5000';
