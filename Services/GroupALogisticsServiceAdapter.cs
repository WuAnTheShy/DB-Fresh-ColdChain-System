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
    IColdChainLogisticsService coldChainLogisticsService) : ILogisticsService
{
    public async Task<decimal> CalculateFreightAsync(
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
                Quantity = item.Quantity
            }).ToList()
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccess || response.Data == null)
            throw new OrderBusinessException($"冷链运费计算失败：{response.Message}");
        if (response.Data.FreightAmount < 0)
            throw new OrderBusinessException("冷链运费不能为负数");
        return response.Data.FreightAmount;
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

            var response = await coldChainLogisticsService.CreateShipmentAsync(new ShipmentRequest
            {
                OrderID = request.OrderId,
                SupplierID = supplierGroup.Key,
                Items = supplierGroup
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
        }
    }

    public async Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await coldChainLogisticsService.GetTraceabilityByOrderAsync(orderId);
        if (!response.IsSuccess)
        {
            if (response.Code == 404)
                return CreatePendingStatuses(supplierIds);
            throw new OrderBusinessException($"冷链履约状态查询失败：{response.Message}");
        }

        var traces = response.Data ?? [];
        var bySupplier = traces
            .GroupBy(trace => trace.SupplierID, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(trace => trace.ShippedAt).First(),
                StringComparer.Ordinal);
        return supplierIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(supplierId => supplierId, StringComparer.Ordinal)
            .Select(supplierId => bySupplier.TryGetValue(supplierId, out var trace)
                ? new SupplierFulfillmentStatus
                {
                    SupplierId = supplierId,
                    StatusName = trace.LogisticsStatus,
                    TrackingNo = trace.TrackingNo
                }
                : new SupplierFulfillmentStatus
                {
                    SupplierId = supplierId,
                    StatusName = "待发货"
                })
            .ToList();
    }

    private static IReadOnlyList<SupplierFulfillmentStatus> CreatePendingStatuses(
        IReadOnlyList<string> supplierIds) =>
        supplierIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(supplierId => supplierId, StringComparer.Ordinal)
            .Select(supplierId => new SupplierFulfillmentStatus
            {
                SupplierId = supplierId,
                StatusName = "待发货"
            })
            .ToList();
}
