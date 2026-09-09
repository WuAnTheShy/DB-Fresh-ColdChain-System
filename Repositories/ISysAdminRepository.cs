using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories
{
    public interface ISysAdminRepository
    {
        // 角色相关
        Task<GroupC_SysRole?> GetRoleByIdAsync(string roleId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsRoleCodeAsync(string roleCode, string? excludeRoleId = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task SaveRoleAsync(GroupC_SysRole role, bool isNew, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);

        // 用户相关
        Task<GroupC_SysUser?> GetUserByIdAsync(string userId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        GroupC_SysUser? GetUserByName(string username);
        Task<List<GroupC_SysUser>> GetAllUsersAsync(IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<List<GroupC_SysUser>> GetUsersByStatusAsync(string status, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<bool> UpdateUserStatusAsync(string userId, string status, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task <bool> ExistsUsernameAsync(string username, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task <bool> SaveUserAsync(GroupC_SysUser user, bool isNew, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);

        // 日志记录
        Task AddLogRecordAsync(GroupC_LogAuditrails logData, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}