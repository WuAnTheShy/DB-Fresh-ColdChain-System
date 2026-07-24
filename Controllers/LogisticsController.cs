using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogisticsController : ControllerBase
{
    private readonly ILogisticsService _service;

    public LogisticsController(ILogisticsService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取待配送订单列表
    /// </summary>
    [HttpGet("pending")]
    public async Task<ApiResponse<List<OrderResultDto>>> GetPending()
        => await _service.GetPendingDeliveryOrdersAsync();

    /// <summary>
    /// 开始配送
    /// </summary>
    [HttpPost("{orderId}/start")]
    public async Task<ApiResponse> StartDelivery(int orderId)
        => await _service.StartDeliveryAsync(orderId);

    /// <summary>
    /// 完成配送
    /// </summary>
    [HttpPost("{orderId}/complete")]
    public async Task<ApiResponse> CompleteDelivery(int orderId)
        => await _service.CompleteDeliveryAsync(orderId);

    /// <summary>
    /// 某团长的配送汇总
    /// </summary>
    [HttpGet("summary/{leaderId}")]
    public async Task<ApiResponse<List<OrderResultDto>>> GetSummary(int leaderId)
        => await _service.GetDeliverySummaryByLeaderAsync(leaderId);
}
