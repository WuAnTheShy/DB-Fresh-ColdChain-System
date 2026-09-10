using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// B 组调用 A 组运费与物流模块的跨组契约。
// A 组实现必须使用传入事务，不能自行提交或回滚。
public interface ILogisticsService
{
    Task<FreightCalculationResult> QuoteFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> CreateSupplierShipmentAsync(
        FulfillmentOrderRequest request,
        SupplierShipmentCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierLogisticsSnapshot>> GetSupplierLogisticsAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
        LogisticsTrackingEventCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
