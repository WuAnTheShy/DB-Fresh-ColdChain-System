-- 20260906 管理员账号按种类细分
-- 适用：Oracle（表名/字段与代码模型一致）
--
-- 说明：SYS_USERS 新增 ADMIN_KIND 列，取值：
--   ACCOUNT 账号管理员 / FINANCE 财务管理员 / LOG 日志管理员 / PRODUCT 商品管理员
-- 存量管理员账号默认按「账号管理员」处理（后续可在登录/审核流程中再调整）。
-- 列已存在时本脚本静默跳过，可重复执行。
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_USERS ADD ADMIN_KIND VARCHAR2(30) DEFAULT ''ACCOUNT''';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE <> -01430 THEN RAISE; END IF; -- ORA-01430: 列已存在
END;
/
