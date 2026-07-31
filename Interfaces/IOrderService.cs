using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// 订单管理服务接口
/// </summary>
public interface IOrderService
{
    Task<ApiResponse<OrderResultDto>> CreateOrderAsync(CreateOrderDto dto);
    Task<ApiResponse<OrderResultDto>> GetOrderDetailAsync(int orderId);
    Task<ApiResponse<PagedResult<OrderResultDto>>> GetOrdersAsync(int pageIndex, int pageSize, int? status = null);
    Task<ApiResponse<List<OrderResultDto>>> GetOrdersByLeaderAsync(int leaderId);
    Task<ApiResponse> UpdateOrderStatusAsync(int orderId, int newStatus);
    Task<ApiResponse> CancelOrderAsync(int orderId);
}
