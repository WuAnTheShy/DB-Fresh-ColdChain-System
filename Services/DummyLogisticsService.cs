using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>
/// A 组正式适配前使用的物流 Dummy，不访问 A 组数据表。
/// </summary>
public sealed class DummyLogisticsService : ILogisticsService
{
    public Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0m);
    }

    public Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        int orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supplierIds);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SupplierFulfillmentStatus> result = supplierIds
            .Distinct()
            .OrderBy(supplierId => supplierId)
            .Select(supplierId => new SupplierFulfillmentStatus
            {
                SupplierId = supplierId,
                StatusName = "待 A 组物流适配"
            })
            .ToList();
        return Task.FromResult(result);
    }
}
