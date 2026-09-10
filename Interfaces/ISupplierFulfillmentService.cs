using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// B 组供应商履约编排入口，只读写 B 组订单并通过物流契约调用 A 组。
public interface ISupplierFulfillmentService
{
    Task<SupplierFulfillmentListViewModel> GetOrdersAsync(
        string supplierId,
        SupplierFulfillmentQuery query,
        CancellationToken cancellationToken = default);

    Task<SupplierFulfillmentDetailViewModel?> GetOrderAsync(
        string supplierId,
        string orderId,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> ShipAsync(
        string supplierId,
        string orderId,
        SupplierShipmentCommand command,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
        string supplierId,
        string orderId,
        LogisticsTrackingEventCommand command,
        CancellationToken cancellationToken = default);
}
