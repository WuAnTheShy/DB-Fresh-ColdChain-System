using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<OrderResultDto>> Create([FromBody] CreateOrderDto dto)
        => await _orderService.CreateOrderAsync(dto);

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<OrderResultDto>> GetDetail(int id)
        => await _orderService.GetOrderDetailAsync(id);

    /// <summary>
    /// 分页获取订单列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<OrderResultDto>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? status = null)
        => await _orderService.GetOrdersAsync(pageIndex, pageSize, status);

    /// <summary>
    /// 获取某团长的订单
    /// </summary>
    [HttpGet("by-leader/{leaderId}")]
    public async Task<ApiResponse<List<OrderResultDto>>> GetByLeader(int leaderId)
        => await _orderService.GetOrdersByLeaderAsync(leaderId);

    /// <summary>
    /// 更新订单状态
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ApiResponse> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        => await _orderService.UpdateOrderStatusAsync(id, dto.Status);

    /// <summary>
    /// 取消订单
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ApiResponse> Cancel(int id)
        => await _orderService.CancelOrderAsync(id);
}
