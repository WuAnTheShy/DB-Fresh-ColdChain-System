using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Order;

/// <summary>
/// 订单仓储接口
/// </summary>
public interface IOrderRepository : IBaseRepository<Models.Order>
{
    Task<Models.Order?> GetOrderDetailAsync(int orderId);
    Task<List<Models.Order>> GetOrdersByLeaderIdAsync(int leaderId);
    Task<List<Models.Order>> GetOrdersByStatusAsync(int status);
    Task<(List<Models.Order> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, int? status = null);
}
