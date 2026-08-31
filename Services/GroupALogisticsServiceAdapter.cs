using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 将 A 组真实冷链报价、FEFO 发货与溯源实现适配为 B 组订单契约。
/// </summary>
public sealed class GroupALogisticsServiceAdapter(
    IUnitOfWork unitOfWork,
    IColdChainLogisticsService coldChainLogisticsService,
    ILogExpressDeliveryRepository deliveryRepository) : ILogisticsService
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
        var deliveries = await deliveryRepository.GetByOrderIdAsync(orderId);
        var bySupplier = deliveries
            .GroupBy(delivery => delivery.SupplierID, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(delivery => delivery.ShippedAt).First(),
                StringComparer.Ordinal);

        return supplierIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(supplierId => supplierId, StringComparer.Ordinal)
            .Select(supplierId => bySupplier.TryGetValue(supplierId, out var delivery)
                ? new SupplierFulfillmentStatus
                {
                    SupplierId = supplierId,
                    StatusName = delivery.LogisticsStatus,
                    TrackingNo = delivery.TrackingNo
                }
                : new SupplierFulfillmentStatus
                {
                    SupplierId = supplierId,
                    StatusName = "待发货"
                })
            .ToList();
    }
}
