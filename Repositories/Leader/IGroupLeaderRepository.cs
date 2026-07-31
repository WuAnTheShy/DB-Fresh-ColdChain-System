using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Leader;

/// <summary>
/// 团长仓储接口
/// </summary>
public interface IGroupLeaderRepository : IBaseRepository<GroupLeader>
{
    Task<GroupLeader?> GetLeaderWithOrdersAsync(int leaderId);
    Task<(List<GroupLeader> Items, int Total)> GetPagedWithOrderCountAsync(int pageIndex, int pageSize);
}
