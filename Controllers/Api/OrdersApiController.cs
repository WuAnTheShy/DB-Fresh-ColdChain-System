using System.ComponentModel.DataAnnotations;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/orders")]
public sealed class OrdersApiController(
    IOrderService orderService,
    ICustomerService customerService,
    IColdChainLogisticsService coldChainLogisticsService,
    IProductEvaluationService productEvaluationService) : GroupBApiController
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
                order.PromoterId,
                order.PromoterName,
                order.CustomerId,
                order.CustomerName,
                order.FinalAmount,
                order.OrderStatus,
                order.ItemCount,
                order.FirstProductId,
                order.FirstProductName,
                order.FirstProductImageUrl,
                order.FirstProductQuantity,
                order.CreatedAt,
                order.DisplayStatusCode,
                order.StatusName
            }),
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("batches/{checkoutBatchId}")]
    public async Task<IActionResult> GetCheckoutBatch(string checkoutBatchId)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();

        var result = await orderService.GetCheckoutBatchAsync(checkoutBatchId, customerId);
        return result == null ? ApiNotFound("结算批次不存在") : Ok(result);
    }

    [HttpPost("batches/{checkoutBatchId}/pay")]
    public async Task<IActionResult> PayCheckoutBatch(
        string checkoutBatchId,
        CheckoutBatchPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();

        var result = await orderService.PayCheckoutBatchAsync(
            checkoutBatchId,
            customerId,
            request,
            cancellationToken);
        return result.IsExpired
            ? Conflict(new { message = "支付已超过15分钟，整个结算批次已关闭", result })
            : Ok(result);
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
        var evaluatedDetailIds = await productEvaluationService.GetEvaluatedOrderDetailIdsAsync(
            detail.Details.Select(item => item.OrderDetailId));
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
                order.PointsUsed,
                order.PointsDiscountAmount,
                order.OrderStatus,
                order.PaymentExpiresAt,
                order.CreatedAt,
                order.UpdatedAt
            },
            detail.CustomerName,
            details = detail.Details.Select(item =>
            {
                var package = detail.SupplierGroups.FirstOrDefault(group =>
                    group.Items.Any(groupItem =>
                        groupItem.OrderDetailId == item.OrderDetailId));
                return new
                {
                    item.OrderDetailId,
                    item.OrderId,
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.SubTotal,
                    item.ReceiptStatus,
                    item.ReceivedAt,
                    isEvaluated = evaluatedDetailIds.Contains(item.OrderDetailId),
                    canEvaluate = string.Equals(item.ReceiptStatus, "RECEIVED", StringComparison.Ordinal) &&
                        !evaluatedDetailIds.Contains(item.OrderDetailId),
                    canConfirmReceipt = order.OrderStatus == OrderStatusCodes.Shipped &&
                        package?.Logistics.StatusCode ==
                        LogisticsStatusCodes.Delivered &&
                        !string.Equals(item.ReceiptStatus, "RECEIVED", StringComparison.Ordinal)
                };
            }),
            packages = detail.SupplierGroups.Select((group, index) => new
            {
                packageNumber = index + 1,
                group.SubTotal,
                itemIds = group.Items.Select(item => item.OrderDetailId),
                logistics = new
                {
                    group.Logistics.CarrierCode,
                    group.Logistics.CarrierName,
                    group.Logistics.TrackingNo,
                    group.Logistics.PackageTemperature,
                    group.Logistics.StatusCode,
                    group.Logistics.StatusName,
                    group.Logistics.ShippedAt,
                    group.Logistics.EstimatedArrivalAt,
                    group.Logistics.DeliveredAt,
                    group.Logistics.HasException,
                    group.Logistics.ExceptionMessage,
                    group.Logistics.DataSource,
                    group.Logistics.IsFallback,
                    events = group.Logistics.Events.Select(item => new
                    {
                        item.EventId,
                        item.StatusCode,
                        item.StatusName,
                        item.Location,
                        item.Description,
                        item.OccurredAt,
                        item.TemperatureCelsius,
                        item.IsTemperatureException
                    })
                }
            }),
            freightQuote = detail.FreightQuote == null ? null : new
            {
                detail.FreightQuote.SchemaVersion,
                detail.FreightQuote.FreightAmount,
                detail.FreightQuote.GoodsAmount,
                detail.FreightQuote.Province,
                detail.FreightQuote.City,
                detail.FreightQuote.District,
                detail.FreightQuote.RuleSummary,
                detail.FreightQuote.CalculatedAt,
                detail.FreightQuote.DataSource,
                items = detail.FreightQuote.Items.Select(item => new
                {
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.SubTotal
                })
            },
            detail.CanComplete,
            detail.CanCancel,
            detail.DisplayStatusCode,
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

    [HttpPost("freight-quote")]
    public async Task<IActionResult> QuoteFreight(CheckoutFreightQuoteRequest request)
    {
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();
        if (!string.Equals(request.CustomerId, signedInCustomerId, StringComparison.Ordinal))
            return ApiForbidden();

        var address = await customerService.GetAddressForEditAsync(
            signedInCustomerId,
            request.AddressId);
        if (address == null) return ApiNotFound("收货地址不存在或不属于当前消费者");

        // 交易身份 = (团长, 商品, 供应商)：同一商品不同供应商作为独立条目计算运费
        var normalizedItems = request.Items
            .GroupBy(item => new { item.PromoterId, item.ProductId, item.SupplierId })
            .Select(group => new
            {
                group.Key.PromoterId,
                group.Key.ProductId,
                group.Key.SupplierId,
                Quantity = group.Sum(item => item.Quantity),
                ClientUnitPrice = group.First().ClientUnitPrice
            })
            .ToList();
        if (normalizedItems.Count == 0 || normalizedItems.Any(item =>
                string.IsNullOrWhiteSpace(item.PromoterId) ||
                string.IsNullOrWhiteSpace(item.ProductId) ||
                string.IsNullOrWhiteSpace(item.SupplierId) ||
                item.Quantity <= 0 ||
                item.ClientUnitPrice is not > 0))
            return ApiBadRequest("运费计算商品信息无效");

        decimal freightAmount = 0;
        var groupQuotes = new List<object>();
        foreach (var promoterGroup in normalizedItems
                     .GroupBy(item => item.PromoterId, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var goodsAmount = promoterGroup.Sum(item =>
                item.ClientUnitPrice!.Value * item.Quantity);
            var quote = await coldChainLogisticsService.QuoteFreightAsync(new FreightQuoteRequest
            {
                Province = address.Province,
                City = address.City,
                District = address.District,
                GoodsAmount = goodsAmount,
                Items = promoterGroup.Select(item => new FreightItemDto
                {
                    ProductID = item.ProductId,
                    SupplierID = item.SupplierId,
                    Quantity = item.Quantity
                }).ToList()
            });
            if (!quote.IsSuccess || quote.Data == null)
                return ApiBadRequest($"冷链运费计算失败：{quote.Message}");

            freightAmount += quote.Data.FreightAmount;
            groupQuotes.Add(new
            {
                promoterId = promoterGroup.Key,
                freightAmount = quote.Data.FreightAmount,
                quote.Data.RuleSummary
            });
        }

        return Ok(new
        {
            freightAmount = decimal.Round(freightAmount, 2),
            groups = groupQuotes
        });
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

    [HttpPost("{orderId}/items/{orderDetailId}/confirm-receipt")]
    public async Task<IActionResult> ConfirmItemReceipt(
        string orderId,
        string orderDetailId,
        CancellationToken cancellationToken)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();
        var authorizationError = await AuthorizeOrderAsync(orderId);
        if (authorizationError != null) return authorizationError;

        await orderService.ConfirmOrderItemReceiptAsync(
            orderId,
            orderDetailId,
            customerId,
            cancellationToken);
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
