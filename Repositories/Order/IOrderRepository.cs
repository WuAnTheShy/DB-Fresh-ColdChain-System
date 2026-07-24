using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Order;

/// <summary>
/// 订单专用仓储接口
/// </summary>
public interface IOrderRepository : IBaseRepository<Models.Order>
{
    /// <summary>
    /// 查询订单详情（含明细、团长、产品信息）
    /// </summary>
    Task<Models.Order?> GetOrderDetailAsync(int orderId);

    /// <summary>
    /// 根据团长查询订单
    /// </summary>
    Task<List<Models.Order>> GetOrdersByLeaderIdAsync(int leaderId);

    /// <summary>
    /// 根据状态查询订单
    /// </summary>
    Task<List<Models.Order>> GetOrdersByStatusAsync(int status);
}
