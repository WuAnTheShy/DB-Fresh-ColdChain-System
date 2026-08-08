using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组调用 A 组运费与物流模块的跨组契约。
/// A 组实现必须使用传入事务，不能自行提交或回滚。
/// </summary>
public interface ILogisticsService
{
    Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        int orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default);
}
