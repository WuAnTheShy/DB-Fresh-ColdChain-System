using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 将 A 组公开的冷链服务适配为 B 组订单履约契约。
/// 运费、FEFO 扣减、发货单与溯源查询全部由 A 组服务完成。
/// </summary>
public sealed class GroupALogisticsServiceAdapter(
    IUnitOfWork unitOfWork,
    IColdChainLogisticsService coldChainLogisticsService,
    IGroupALogisticsExtensionProvider extensionProvider) : ILogisticsService
{
    public async Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var quote = await QuoteFreightAsync(request, transaction, cancellationToken);
        return quote.FreightAmount;
    }

    public async Task<FreightCalculationResult> QuoteFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        unitOfWork.AttachExternalTransaction(transaction);

        var response = await coldChainLogisticsService.QuoteFreightAsync(new FreightQuoteRequest
        {
            Province = request.Province,
            City = request.City,
            District = request.District,
            GoodsAmount = request.GoodsAmount,
            Items = request.Items.Select(item => new FreightItemDto
            {
                ProductID = item.ProductId,
                SupplierID = item.SupplierId,
                Quantity = item.Quantity
            }).ToList()
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccess || response.Data == null)
            throw new OrderBusinessException($"冷链运费计算失败：{response.Message}");
        if (response.Data.FreightAmount < 0)
            throw new OrderBusinessException("冷链运费不能为负数");

        var sourceItems = response.Data.Items.ToDictionary(
            item => (item.ProductID, item.SupplierID));
        return new FreightCalculationResult
        {
            FreightAmount = response.Data.FreightAmount,
            GoodsAmount = request.GoodsAmount,
            Province = request.Province,
            City = request.City,
            District = request.District,
            RuleSummary = response.Data.RuleSummary,
            CalculatedAt = DateTime.Now,
            DataSource = LogisticsDataSources.GroupA,
            Items = request.Items.Select(item =>
            {
                sourceItems.TryGetValue((item.ProductId, item.SupplierId), out var quoted);
                return new FreightCalculationItemResult
                {
                    ProductId = item.ProductId,
                    ProductName = quoted?.ProductName ?? item.ProductName,
                    SupplierId = item.SupplierId,
                    Quantity = item.Quantity,
                    UnitPrice = quoted?.UnitPrice ?? item.UnitPrice,
                    SubTotal = quoted != null ? quoted.UnitPrice * item.Quantity : item.SubTotal
                };
            }).ToList()
        };
    }

    public async Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        unitOfWork.AttachExternalTransaction(transaction);

        foreach (var supplierGroup in request.Items
                     .GroupBy(item => item.SupplierId, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(supplierGroup.Key))
                throw new OrderBusinessException("发货商品缺少供应商");

            await CreateSupplierShipmentAsync(
                request,
                new SupplierShipmentCommand
                {
                    SupplierId = supplierGroup.Key
                },
                transaction,
                cancellationToken);
        }
    }

    public async Task<SupplierLogisticsSnapshot> CreateSupplierShipmentAsync(
        FulfillmentOrderRequest request,
        SupplierShipmentCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        unitOfWork.AttachExternalTransaction(transaction);

        var supplierItems = request.Items
            .Where(item => string.Equals(
                item.SupplierId,
                command.SupplierId,
                StringComparison.Ordinal))
            .ToList();
        if (supplierItems.Count == 0)
            throw new OrderBusinessException("发货单不包含当前供应商的商品");

        var response = await coldChainLogisticsService.CreateShipmentAsync(new ShipmentRequest
        {
            OrderID = request.OrderId,
            SupplierID = command.SupplierId,
            Items = supplierItems
                .GroupBy(item => item.ProductId, StringComparer.Ordinal)
                .Select(group => new FreightItemDto
                {
                    ProductID = group.Key,
                    Quantity = checked(group.Sum(item => item.Quantity))
                })
                .ToList()
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccess || response.Data == null)
            throw new OrderBusinessException($"冷链发货失败：{response.Message}");

        return await extensionProvider.RegisterShipmentAsync(
            new LogisticsShipmentRegistration
            {
                DeliveryId = response.Data.DeliveryID,
                OrderId = request.OrderId,
                SupplierId = command.SupplierId,
                BaseTrackingNo = response.Data.TrackingNo,
                BaseStatus = response.Data.LogisticsStatus,
                ShippedAt = response.Data.ShippedAt,
                Command = command
            },
            transaction,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await GetSupplierLogisticsAsync(
            orderId,
            supplierIds,
            cancellationToken);
        return snapshots
            .Select(snapshot => new SupplierFulfillmentStatus
            {
                SupplierId = snapshot.SupplierId,
                StatusName = snapshot.StatusCode == LogisticsStatusCodes.Pending
                    ? "待发货"
                    : snapshot.StatusCode,
                TrackingNo = snapshot.TrackingNo
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SupplierLogisticsSnapshot>> GetSupplierLogisticsAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await coldChainLogisticsService.GetTraceabilityByOrderAsync(orderId);
        if (!response.IsSuccess && response.Code != 404)
            throw new OrderBusinessException($"冷链履约状态查询失败：{response.Message}");

        var traces = response.IsSuccess ? response.Data ?? [] : [];
        var bySupplier = traces
            .GroupBy(trace => trace.SupplierID, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(trace => trace.ShippedAt).First(),
                StringComparer.Ordinal);
        var snapshots = new List<SupplierLogisticsSnapshot>();
        foreach (var supplierId in supplierIds
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            bySupplier.TryGetValue(supplierId, out var trace);
            snapshots.Add(await extensionProvider.GetSnapshotAsync(
                new LogisticsTraceSeed
                {
                    OrderId = orderId,
                    SupplierId = supplierId,
                    DeliveryId = trace?.DeliveryID,
                    TrackingNo = trace?.TrackingNo,
                    StatusCode = trace?.LogisticsStatus ?? LogisticsStatusCodes.Pending,
                    ShippedAt = trace?.ShippedAt
                },
                cancellationToken));
        }

        return snapshots;
    }

    public Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
        LogisticsTrackingEventCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        unitOfWork.AttachExternalTransaction(transaction);
        return extensionProvider.AppendTrackingEventAsync(
            command,
            transaction,
            cancellationToken);
    }
}
