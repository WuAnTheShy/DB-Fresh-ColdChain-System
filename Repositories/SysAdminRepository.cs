using Dapper;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models.DTOs;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public class SysAdminRepository : ISysAdminRepository
    {
        private readonly IUnitOfWork _uow;
        private readonly ITableLogRepository _itableLogRepository;

        public SysAdminRepository(IUnitOfWork uow, ITableLogRepository itableLogRepository)
        {
            _uow = uow;
            _itableLogRepository = itableLogRepository;
        }

        #region 角色相关

        public async Task<GroupC_SysRole?> GetRoleByIdAsync(
            string roleId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = "SELECT * FROM SYS_ROLES WHERE ROLEID = :RoleId";
            return await _uow.Connection.QueryFirstOrDefaultAsync<GroupC_SysRole>(
                sql,
                new { RoleId = roleId },
                transaction);
        }

        public async Task<bool> ExistsRoleCodeAsync(
            string roleCode,
            string? excludeRoleId = null,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) FROM SYS_ROLES 
                WHERE ROLECODE = :RoleCode 
                AND (:ExcludeRoleId IS NULL OR ROLEID <> :ExcludeRoleId)";

            int count = await _uow.Connection.ExecuteScalarAsync<int>(
                sql,
                new { RoleCode = roleCode, ExcludeRoleId = excludeRoleId },
                transaction);
            return count > 0;
        }

        public async Task SaveRoleAsync(
            GroupC_SysRole role,
            bool isNew,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = isNew
                ? @"INSERT INTO SYS_ROLES (ROLEID, ROLENAME, ROLECODE, DESCRIPTION, STATUS, CREATETIME)
                   VALUES (:RoleId, :RoleName, :RoleCode, :Description, :Status, :CreateTime)"
                : @"UPDATE SYS_ROLES 
                   SET ROLENAME = :RoleName, ROLECODE = :RoleCode, DESCRIPTION = :Description, STATUS = :Status 
                   WHERE ROLEID = :RoleId";

            await _uow.Connection.ExecuteAsync(sql, role, transaction);
        } 

        #endregion

        #region 用户相关

        public async Task<GroupC_SysUser?> GetUserByIdAsync(
            string userId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = "SELECT * FROM SYS_USERS WHERE USERID = :UserId";
            return await _uow.Connection.QueryFirstOrDefaultAsync<GroupC_SysUser>(
                sql,
                new { UserId = userId },
                transaction);
        }

        public async Task<bool> ExistsUsernameAsync(
            string username,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = "SELECT COUNT(1) FROM SYS_USERS WHERE USERNAME = :Username";
            int count = await _uow.Connection.ExecuteScalarAsync<int>(
                sql,
                new { Username = username },
                transaction);
            return count > 0;
        }

        public async Task SaveUserAsync(
            GroupC_SysUser user,
            bool isNew,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = isNew
                ? @"INSERT INTO SYS_USERS (USERID, USERNAME, PASSWORDHASH, ROLEID, REALNAME, PHONE, STATUS, CREATETIME)
                   VALUES (:UserId, :Username, :PasswordHash, :RoleId, :RealName, :Phone, :Status, :CreateTime)"
                : @"UPDATE SYS_USERS 
                   SET USERNAME = :Username, PASSWORDHASH = :PasswordHash, ROLEID = :RoleId, REALNAME = :RealName, PHONE = :Phone, STATUS = :Status 
                   WHERE USERID = :UserId";

            await _uow.Connection.ExecuteAsync(sql, user, transaction);
        }

        #endregion

        #region 日志记录

        public async Task AddLogRecordAsync(
            GroupC_LogAuditrails logData,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            await _itableLogRepository.GroupC_AddLogRecordAsync(logData);
        }

        #endregion
    }
}