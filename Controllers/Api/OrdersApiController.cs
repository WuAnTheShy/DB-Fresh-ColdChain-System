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
    IProductEvaluationService productEvaluationService,
    IRefundService refundService) : GroupBApiController
{
    // 退款申请状态取值（FIN_REFUND.STATUS，C 组职责，此处仅用于展示映射）
    private const string RefundStatusPending = "Pending";

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] OrderQueryRequest request)
    {
        var signedInCustomerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(signedInCustomerId)) return ApiUnauthorized();
        request.CustomerId = signedInCustomerId;

        // 「退款售后」列表 = 处于退款流程中的订单（退款审核中 / 退款中）；
        // 另外把“存在待审核申请但订单状态未进入审核中”的历史申请单按订单号兜底纳入，
        // 保证消费者提交申请后立刻能查到
        if (request.Status == OrderStatus.Refunding)
        {
            request.OrderStatuses = [OrderStatus.RefundReviewing, OrderStatus.Refunding];
            request.OrderIds = await refundService.GetOrderIdsWithPendingRefundAsync();
        }

        var result = await orderService.GetOrdersAsync(request);
        await ApplyRefundDisplayStatusAsync(result.Orders);
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
                productItems = order.ProductItems.Select(item => new
                {
                    item.OrderDetailId,
                    item.ProductId,
                    item.ProductName,
                    imageUrl = item.ImageUrl,
                    item.Quantity,
                    item.UnitPrice,
                    item.SubTotal
                }),
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
        // 提交退款申请后订单真实状态即为“退款审核中”，此处对改造前提交、订单状态未进入
        // 审核中的历史申请单做同样的展示兜底
        var refunds = await refundService.GetOrderRefundsAsync(orderId);
        if (refunds.Any(refund => string.Equals(refund.Status, RefundStatusPending, StringComparison.Ordinal)))
        {
            detail.DisplayStatusCode = OrderStatusCodes.RefundReviewing;
            detail.StatusName = OrderStatusNames.GetName(OrderStatus.RefundReviewing);
        }

        var evaluations = await productEvaluationService.GetByOrderDetailIdsAsync(
            detail.Details.Select(item => item.OrderDetailId));
        var evaluationsByDetailId = evaluations.ToDictionary(
            item => item.OrderDetailId,
            StringComparer.Ordinal);
        var productImages = detail.ProductItems.ToDictionary(
            item => item.OrderDetailId,
            item => item.ImageUrl,
            StringComparer.Ordinal);
        return Ok(new
        {
            detail.OrderId,
            detail.PromoterName,
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
                evaluationsByDetailId.TryGetValue(item.OrderDetailId, out var evaluation);
                return new
                {
                    item.OrderDetailId,
                    item.OrderId,
                    item.ProductId,
                    item.ProductName,
                    imageUrl = productImages.GetValueOrDefault(item.OrderDetailId),
                    item.Quantity,
                    item.UnitPrice,
                    item.SubTotal,
                    item.ReceiptStatus,
                    item.ReceivedAt,
                    isEvaluated = evaluation != null,
                    evaluationDimensions = GetSelectedEvaluationDimensions(evaluation),
                    evaluatedAt = evaluation?.CreatedAt,
                    canEvaluate = string.Equals(item.ReceiptStatus, "RECEIVED", StringComparison.Ordinal) &&
                        evaluation == null,
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
            detail.StatusName,
            refunds = refunds
                .OrderByDescending(refund => refund.ApplyTime)
                .Select(refund => new
                {
                    refund.RefundId,
                    refund.DetailId,
                    refund.SupplierId,
                    refund.RefundQty,
                    refund.RefundAmount,
                    refund.Status,
                    statusName = GetRefundStatusName(refund.Status),
                    refund.ApplyTime,
                    refund.AuditTime,
                    refund.Remark
                })
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

    [HttpPost("{orderId}/confirm-receipt")]
    public async Task<IActionResult> ConfirmOrderReceipt(
        string orderId,
        CancellationToken cancellationToken)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();
        var authorizationError = await AuthorizeOrderAsync(orderId);
        if (authorizationError != null) return authorizationError;

        await orderService.ConfirmOrderReceiptAsync(orderId, customerId, cancellationToken);
        return NoContent();
    }

    // 订单列表叠加退款展示状态：一次批量查询当前页订单的退款申请，
    // 存在待审核申请即展示为“退款审核中”（新申请单的订单真实状态已是 REFUND_REVIEWING，
    // 此叠加主要覆盖改造前提交的历史申请单）
    private async Task ApplyRefundDisplayStatusAsync(IReadOnlyCollection<OrderListItem> orders)
    {
        if (orders.Count == 0)
        {
            return;
        }

        var refunds = await refundService.GetOrderRefundsAsync(
            orders.Select(order => order.OrderId).ToArray());
        var pendingOrderIds = refunds
            .Where(refund => string.Equals(refund.Status, RefundStatusPending, StringComparison.Ordinal))
            .Select(refund => refund.OrderId)
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Select(orderId => orderId!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            if (!pendingOrderIds.Contains(order.OrderId))
            {
                continue;
            }

            order.DisplayStatusCode = OrderStatusCodes.RefundReviewing;
            order.DisplayStatusName = OrderStatusNames.GetName(OrderStatus.RefundReviewing);
        }
    }

    private static string GetRefundStatusName(string? status) => status switch
    {
        RefundStatusPending => "退款审核中",
        "Approved" => "退款已通过",
        "Rejected" => "退款已驳回",
        "Cancelled" => "已取消申请",
        _ => status ?? string.Empty
    };

    private static IReadOnlyList<string> GetSelectedEvaluationDimensions(ProductEvaluation? evaluation)
    {
        if (evaluation == null) return [];
        var selected = new List<string>();
        if (evaluation.HighQuality == 1) selected.Add(ProductEvaluationDimensions.HighQuality);
        if (evaluation.FastShipping == 1) selected.Add(ProductEvaluationDimensions.FastShipping);
        if (evaluation.GoodPackaging == 1) selected.Add(ProductEvaluationDimensions.GoodPackaging);
        if (evaluation.CostEffective == 1) selected.Add(ProductEvaluationDimensions.CostEffective);
        if (evaluation.Affordable == 1) selected.Add(ProductEvaluationDimensions.Affordable);
        if (evaluation.ReliablePromoter == 1) selected.Add(ProductEvaluationDimensions.ReliablePromoter);
        return selected;
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
