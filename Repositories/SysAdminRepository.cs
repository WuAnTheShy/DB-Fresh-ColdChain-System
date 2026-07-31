using Dapper;
using DBFreshColdChain.Models;

namespace DBFreshColdChain.Repositories
{
    public class SysAdminRepository
    {
        private readonly DbHelper _dbHelper;

        public SysAdminRepository(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        #region

        public SysRole? GetRoleById(string roleId)//根据RoleID查找对应角色
        {
            string sql = "SELECT * FROM SYS_ROLES WHERE ROLEID = :RoleId";
            using var conn = _dbHelper.GetConnection();
            return conn.QueryFirstOrDefault<SysRole>(sql, new { RoleId = roleId });
        }

        public bool ExistsRoleCode(string roleCode, string? excludeRoleId = null)
        {
            string sql = "SELECT COUNT(1) FROM SYS_ROLES WHERE ROLECODE = :RoleCode AND (:ExcludeRoleId IS NULL OR ROLEID <> :ExcludeRoleId)";
            using var conn = _dbHelper.GetConnection();
            return conn.ExecuteScalar<int>(sql, new { RoleCode = roleCode, ExcludeRoleId = excludeRoleId }) > 0;
        }

        /// <summary>
        /// 通用 Save 方法：isNew=true 执行插入，isNew=false 执行全字段更新
        /// </summary>
        public void SaveRole(SysRole role, bool isNew)
        {
            string sql = isNew
                ? @"INSERT INTO SYS_ROLES (ROLEID, ROLENAME, ROLECODE, DESCRIPTION, STATUS, CREATETIME)
                    VALUES (:RoleId, :RoleName, :RoleCode, :Description, :Status, :CreateTime)"
                : @"UPDATE SYS_ROLES 
                    SET ROLENAME = :RoleName, ROLECODE = :RoleCode, DESCRIPTION = :Description, STATUS = :Status 
                    WHERE ROLEID = :RoleId";

            using var conn = _dbHelper.GetConnection();
            conn.Execute(sql, role);
        }

        #endregion

        #region 

        public SysUser? GetUserById(string userId)//根据UserID查找用户
        {
            string sql = "SELECT * FROM SYS_USERS WHERE USERID = :UserId";
            using var conn = _dbHelper.GetConnection();
            return conn.QueryFirstOrDefault<SysUser>(sql, new { UserId = userId });
        }

        public bool ExistsUsername(string username)//检查用户名是否存在
        {
            string sql = "SELECT COUNT(1) FROM SYS_USERS WHERE USERNAME = :Username";
            using var conn = _dbHelper.GetConnection();
            return conn.ExecuteScalar<int>(sql, new { Username = username }) > 0;
        }

        /// <summary>
        /// 通用 Save 方法：更新密码/角色/状态都直接通过内存赋值后调用此方法
        /// </summary>
        public void SaveUser(SysUser user, bool isNew)
        {
            string sql = isNew
                ? @"INSERT INTO SYS_USERS (USERID, USERNAME, PASSWORDHASH, ROLEID, REALNAME, PHONE, STATUS, CREATETIME)
                    VALUES (:UserId, :Username, :PasswordHash, :RoleId, :RealName, :Phone, :Status, :CreateTime)"
                : @"UPDATE SYS_USERS 
                    SET USERNAME = :Username, PASSWORDHASH = :PasswordHash, ROLEID = :RoleId, REALNAME = :RealName, PHONE = :Phone, STATUS = :Status 
                    WHERE USERID = :UserId";

            using var conn = _dbHelper.GetConnection();
            conn.Execute(sql, user);
        }

        #endregion

        #region 

        public void AddLogRecord(Log_Auditrails logData)//日志记录更新
        {
            _dbHelper.GroupC_AddLogRecord(logData);
        }

        #endregion
    }
}