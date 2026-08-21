using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>
/// A 组正式适配完成前使用的库存 Dummy。
/// 仅提供确定性商品快照和库存校验，不直接访问任何 A 组数据表。
/// </summary>
public sealed class DummyInventoryService : IInventoryService
{
    private static readonly IReadOnlyDictionary<string, DummyProduct> Products =
        new Dictionary<string, DummyProduct>(StringComparer.Ordinal)
        {
            ["P1"] = new("车厘子", "SUP1", 50m, 100),
            ["P2"] = new("三文鱼", "SUP2", 80m, 50),
            ["P3"] = new("有机蔬菜", "SUP1", 20m, 200)
        };

    public Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = new List<InventoryProductSnapshot>(items.Count);
        foreach (var item in items)
        {
            if (!Products.TryGetValue(item.ProductId, out var product))
                throw new OrderBusinessException($"商品 {item.ProductId} 不存在或已下架");

            if (item.Quantity > product.AvailableQuantity)
            {
                throw new OrderBusinessException(
                    $"商品“{product.ProductName}”库存不足，当前可用 {product.AvailableQuantity}");
            }

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = item.ProductId,
                ProductName = product.ProductName,
                SupplierId = product.SupplierId,
                UnitPrice = product.UnitPrice
            });
        }

        return Task.FromResult<IReadOnlyList<InventoryProductSnapshot>>(snapshots);
    }

    public Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private sealed record DummyProduct(
        string ProductName,
        string SupplierId,
        decimal UnitPrice,
        int AvailableQuantity);
}
