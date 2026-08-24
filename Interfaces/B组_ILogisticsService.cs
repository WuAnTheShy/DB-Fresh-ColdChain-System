using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组调用 A 组运费与物流模块的跨组契约。
/// A 组实现必须使用传入的事务，不能自行提交或回滚。
/// </summary>
public interface ILogisticsService
{
    /// <summary>计算冷链运费</summary>
    Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>FEFO 批次扣减 + 创建物流发货单 + 批次溯源</summary>
    Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>查询供应商履约状态（只读）</summary>
    Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default);
}
