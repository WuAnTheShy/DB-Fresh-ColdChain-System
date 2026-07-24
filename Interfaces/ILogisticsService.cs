using FreshGroupSystem.Common;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// 物流配送服务接口
/// </summary>
public interface ILogisticsService
{
    /// <summary>
    /// 获取待配送的订单列表
    /// </summary>
    Task<ApiResponse<List<OrderResultDto>>> GetPendingDeliveryOrdersAsync();

    /// <summary>
    /// 标记订单开始配送
    /// </summary>
    Task<ApiResponse> StartDeliveryAsync(int orderId);

    /// <summary>
    /// 标记订单配送完成
    /// </summary>
    Task<ApiResponse> CompleteDeliveryAsync(int orderId);

    /// <summary>
    /// 获取某团长的配送汇总
    /// </summary>
    Task<ApiResponse<List<OrderResultDto>>> GetDeliverySummaryByLeaderAsync(int leaderId);
}
