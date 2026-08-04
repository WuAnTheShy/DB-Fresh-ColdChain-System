using System.ComponentModel.DataAnnotations;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/orders")]
public sealed class OrdersApiController(
    IOrderService orderService) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] OrderQueryRequest request)
    {
        var result = await orderService.GetOrdersAsync(request);
        return Ok(new
        {
            result.Query,
            orders = result.Orders.Select(order => new
            {
                order.OrderId,
                order.OrderNo,
                order.CustomerId,
                order.CustomerName,
                order.FinalAmount,
                order.OrderStatus,
                order.ItemCount,
                order.CreatedAt,
                order.StatusName
            }),
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetOrder(int orderId)
    {
        var detail = await orderService.GetOrderDetailAsync(orderId);
        if (detail?.Order == null)
            return ApiNotFound("订单不存在");

        var order = detail.Order;
        return Ok(new
        {
            detail.OrderId,
            order = new
            {
                order.OrderId,
                order.OrderNo,
                order.CustomerId,
                order.PromoterId,
                order.AddressId,
                order.ReceiverName,
                order.ReceiverPhone,
                order.ShippingAddress,
                order.TotalAmount,
                order.DiscountAmount,
                order.FreightAmount,
                order.FinalAmount,
                order.PointsEarned,
                order.OrderStatus,
                order.CreatedAt,
                order.UpdatedAt
            },
            detail.CustomerName,
            details = detail.Details.Select(item => new
            {
                item.OrderDetailId,
                item.OrderId,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.SubTotal
            }),
            detail.CanComplete,
            detail.CanCancel,
            detail.StatusName
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await orderService.CreateOrderAsync(
            request,
            cancellationToken);
        return CreatedAtAction(
            nameof(GetOrder),
            new { orderId = result.OrderId },
            new
            {
                result.OrderId,
                result.OrderNo,
                result.GoodsAmount,
                result.DiscountAmount,
                result.FreightAmount,
                result.FinalAmount,
                result.PointsEarned
            });
    }

    [HttpPost("{orderId:int}/transition")]
    public async Task<IActionResult> TransitionOrder(
        int orderId,
        OrderTransitionRequest request,
        CancellationToken cancellationToken)
    {
        await orderService.TransitionOrderAsync(
            orderId,
            request.TargetStatus!.Value,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{orderId:int}/cancel")]
    public async Task<IActionResult> CancelOrder(
        int orderId,
        CancellationToken cancellationToken)
    {
        await orderService.CancelOrderAsync(orderId, cancellationToken);
        return NoContent();
    }
}

public sealed class OrderTransitionRequest
{
    [Required(ErrorMessage = "请选择目标状态")]
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "目标状态无效")]
    public OrderStatus? TargetStatus { get; set; }
}
