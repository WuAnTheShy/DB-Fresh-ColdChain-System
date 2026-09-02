using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// B 组供应商履约编排：校验订单和供应商归属，调用 A 组发货，
/// 并在所有供应商均已发货后推进 B 组订单状态。
/// </summary>
public sealed class SupplierFulfillmentService(
    IOrderRepository orderRepository,
    ILogisticsService logisticsService,
    IOrderTransactionManager transactionManager) : ISupplierFulfillmentService
{
    public async Task<SupplierFulfillmentListViewModel> GetOrdersAsync(
        string supplierId,
        SupplierFulfillmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateSupplierId(supplierId);
        ArgumentNullException.ThrowIfNull(query);
        NormalizeQuery(query);
        cancellationToken.ThrowIfCancellationRequested();

        var totalCount = await orderRepository.CountSupplierFulfillmentOrdersAsync(
            supplierId,
            query);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);
        if (totalPages > 0 && query.Page > totalPages)
            query.Page = totalPages;
        var offset = (query.Page - 1) * query.PageSize;

        return new SupplierFulfillmentListViewModel
        {
            SupplierId = supplierId,
            Query = query,
            Orders = await orderRepository.GetSupplierFulfillmentOrdersAsync(
                supplierId,
                query,
                offset),
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<SupplierFulfillmentDetailViewModel?> GetOrderAsync(
        string supplierId,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        ValidateSupplierId(supplierId);
        ValidateOrderId(orderId);
        cancellationToken.ThrowIfCancellationRequested();

        var header = await orderRepository.GetDetailHeaderAsync(orderId);
        if (header == null) return null;
        var items = (await orderRepository.GetDetailsAsync(orderId))
            .Where(item => string.Equals(
                item.SupplierId,
                supplierId,
                StringComparison.Ordinal))
            .ToList();
        if (items.Count == 0) return null;

        var logistics = (await logisticsService.GetSupplierLogisticsAsync(
            orderId,
            [supplierId],
            cancellationToken)).Single();
        var status = OrderStatusCodes.Parse(header.OrderStatus);
        return new SupplierFulfillmentDetailViewModel
        {
            SupplierId = supplierId,
            Order = header.ToOrder(),
            CustomerName = header.CustomerName,
            Items = items,
            Logistics = logistics,
            CanShip = (status is OrderStatus.Paid or OrderStatus.Shipped) &&
                !LogisticsStatusCodes.IsShippedOrLater(logistics.StatusCode)
        };
    }

    public Task<SupplierLogisticsSnapshot> ShipAsync(
        string supplierId,
        string orderId,
        SupplierShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateSupplierId(supplierId);
        ValidateOrderId(orderId);
        ArgumentNullException.ThrowIfNull(command);
        if (!string.Equals(supplierId, command.SupplierId, StringComparison.Ordinal))
            throw new OrderBusinessException("发货供应商与当前登录账号不一致");
        if (command.EstimatedArrivalAt.HasValue &&
            command.EstimatedArrivalAt.Value <= DateTime.Now)
            throw new OrderBusinessException("预计送达时间必须晚于当前时间");

        return transactionManager.ExecuteAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = await orderRepository.GetByIdForUpdateAsync(orderId, transaction)
                ?? throw new OrderBusinessException("订单不存在");
            var orderStatus = OrderStatusCodes.Parse(order.OrderStatus);
            if (orderStatus is not (OrderStatus.Paid or OrderStatus.Shipped))
                throw new OrderBusinessException("只有已支付或部分发货订单可以继续履约");

            var details = await orderRepository.GetDetailsAsync(orderId, transaction);
            var supplierItems = details
                .Where(item => string.Equals(
                    item.SupplierId,
                    supplierId,
                    StringComparison.Ordinal))
                .ToList();
            if (supplierItems.Count == 0)
                throw new OrderBusinessException("订单不包含当前供应商的商品");

            var supplierIds = details
                .Select(item => item.SupplierId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            var before = (await logisticsService.GetSupplierLogisticsAsync(
                orderId,
                [supplierId],
                cancellationToken)).Single();
            if (LogisticsStatusCodes.IsShippedOrLater(before.StatusCode))
                return before;

            var shipment = await logisticsService.CreateSupplierShipmentAsync(
                CreateFulfillmentRequest(order, supplierItems),
                command,
                transaction,
                cancellationToken);

            var allStatuses = await logisticsService.GetSupplierLogisticsAsync(
                orderId,
                supplierIds,
                cancellationToken);
            if (orderStatus == OrderStatus.Paid &&
                allStatuses.All(item =>
                    LogisticsStatusCodes.IsShippedOrLater(item.StatusCode)) &&
                !await orderRepository.TryUpdateStatusAsync(
                    orderId,
                    OrderStatus.Paid,
                    OrderStatus.Shipped,
                    transaction))
            {
                throw new OrderBusinessException("订单状态已变化，请刷新后重试");
            }

            return shipment;
        });
    }

    private static FulfillmentOrderRequest CreateFulfillmentRequest(
        BizOrder order,
        IReadOnlyList<BizOrderDetail> details) => new()
    {
        OrderId = order.OrderId,
        OrderNo = order.OrderNo,
        ReceiverName = order.ReceiverName,
        ReceiverPhone = order.ReceiverPhone,
        ShippingAddress = order.ShippingAddress,
        Items = details.Select(detail => new FulfillmentOrderItem
        {
            ProductId = detail.ProductId,
            ProductName = detail.ProductName,
            SupplierId = detail.SupplierId ?? string.Empty,
            Quantity = detail.Quantity,
            UnitPrice = detail.UnitPrice,
            SubTotal = detail.SubTotal
        }).ToList()
    };

    private static void NormalizeQuery(SupplierFulfillmentQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 50);
        query.Keyword = string.IsNullOrWhiteSpace(query.Keyword)
            ? null
            : query.Keyword.Trim();
        if (query.Status is not null &&
            query.Status is not (OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Completed))
            throw new OrderBusinessException("供应商履约列表不支持该订单状态");
    }

    private static void ValidateSupplierId(string supplierId)
    {
        if (!GroupBIds.IsValid(supplierId))
            throw new OrderBusinessException("供应商ID格式不正确");
    }

    private static void ValidateOrderId(string orderId)
    {
        if (!GroupBIds.IsValid(orderId))
            throw new OrderBusinessException("订单ID格式不正确");
    }
}
