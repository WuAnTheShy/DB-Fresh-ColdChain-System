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
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();
        request.CustomerId = signedInCustomerId;

        var result = await orderService.GetOrdersAsync(request);
        return Ok(new
        {
            result.Query,
            orders = result.Orders.Select(order => new
            {
                order.OrderId,
                order.OrderNo,
                order.CheckoutBatchId,
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

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();

        var detail = await orderService.GetOrderDetailAsync(orderId);
        if (detail?.Order == null)
            return ApiNotFound("订单不存在");
        if (!string.Equals(detail.Order.CustomerId, signedInCustomerId, StringComparison.Ordinal))
            return ApiForbidden();

        var order = detail.Order;
        return Ok(new
        {
            detail.OrderId,
            order = new
            {
                order.OrderId,
                order.OrderNo,
                order.CustomerId,
                order.CheckoutBatchId,
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
                order.PaymentExpiresAt,
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
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();
        if (!string.Equals(request.CustomerId, signedInCustomerId, StringComparison.Ordinal))
            return ApiForbidden();

        var result = await orderService.CreateCheckoutBatchAsync(
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{orderId}/transition")]
    public async Task<IActionResult> TransitionOrder(
        string orderId,
        OrderTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeOrderAsync(orderId);
        if (authorizationError != null) return authorizationError;

        await orderService.TransitionOrderAsync(
            orderId,
            request.TargetStatus!.Value,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(
        string orderId,
        CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeOrderAsync(orderId);
        if (authorizationError != null) return authorizationError;

        await orderService.CancelOrderAsync(orderId, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult?> AuthorizeOrderAsync(string orderId)
    {
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();

        var detail = await orderService.GetOrderDetailAsync(orderId);
        if (detail?.Order == null) return ApiNotFound("订单不存在");
        return string.Equals(detail.Order.CustomerId, signedInCustomerId, StringComparison.Ordinal)
            ? null
            : ApiForbidden();
    }
}

public sealed class OrderTransitionRequest
{
    [Required(ErrorMessage = "请选择目标状态")]
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "目标状态无效")]
    public OrderStatus? TargetStatus { get; set; }
}
