using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Leader;

public interface IGroupLeaderRepository : IBaseRepository<Models.GroupLeader>
{
    /// <summary>
    /// 获取团长及其所有订单
    /// </summary>
    Task<Models.GroupLeader?> GetLeaderWithOrdersAsync(int leaderId);
}
