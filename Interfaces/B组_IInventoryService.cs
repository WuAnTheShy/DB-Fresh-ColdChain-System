using System.Data;
using FreshGroupSystem.Models.CrossGroup;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// B 组调用 A 组库存模块的跨组契约。
/// A 组实现必须使用传入的事务，不能自行提交或回滚。
/// </summary>
public interface IInventoryService
{
    /// <summary>校验并预留库存，返回订单明细所需的可信商品快照</summary>
    Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>取消未发货订单时释放已预留的库存</summary>
    Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
