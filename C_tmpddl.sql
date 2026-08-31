
-- ============================================================
-- 第二部分：Crm_Promoters 新增 Avatar 列（团长）
-- ============================================================

-- 4. 新增预制头像标识列（可空，不影响旧数据）
ALTER TABLE Crm_Promoters ADD (
    Avatar VARCHAR2(50)   -- 预制头像标识：cat/rabbit/panda/fox/carrot/broccoli/tomato/corn
);

-- 5. 给列加注释，便于维护
COMMENT ON COLUMN Crm_Promoters.Avatar
    IS '预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn）';

-- 6.（可选）给指定团长设置一个头像，方便直接看到效果
-- 请将 '你的团长登录账号' 替换为实际账号，或改用 WHERE PROMOTERID = '...'
UPDATE Crm_Promoters
   SET Avatar = 'panda'
 WHERE LOGINACCOUNT = '你的团长登录账号';

COMMIT;
