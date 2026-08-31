using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// A 组库存能力对 B 组订单契约的正式适配。
/// 所有库存锁定与释放均复用 B 组传入的 Oracle 事务。
/// </summary>
public sealed class GroupAInventoryServiceAdapter(
    IUnitOfWork unitOfWork,
    IProductRepository productRepository,
    IStockSummaryRepository stockSummaryRepository) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transaction);
        if (items.Count == 0)
            throw new OrderBusinessException("库存预留商品不能为空");

        unitOfWork.AttachExternalTransaction(transaction);
        var normalized = items
            .GroupBy(item => item.ProductId?.Trim() ?? string.Empty, StringComparer.Ordinal)
            .Select(group => new InventoryReservationItem
            {
                ProductId = group.Key,
                Quantity = checked(group.Sum(item => item.Quantity))
            })
            .OrderBy(item => item.ProductId, StringComparer.Ordinal)
            .ToList();

        var snapshots = new List<InventoryProductSnapshot>(normalized.Count);
        foreach (var item in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity is <= 0 or > 9999)
                throw new OrderBusinessException("库存预留商品或数量无效");

            var product = await productRepository.GetByIdAsync(item.ProductId)
                ?? throw new OrderBusinessException($"商品 {item.ProductId} 不存在");
            if (!string.Equals(product.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                throw new OrderBusinessException($"商品“{product.ProductName}”已下架");
            if (string.IsNullOrWhiteSpace(product.SupplierID))
                throw new OrderBusinessException($"商品“{product.ProductName}”缺少供应商");
            if (product.DefaultPrice <= 0)
                throw new OrderBusinessException($"商品“{product.ProductName}”价格无效");

            var stock = await stockSummaryRepository.GetByProductIdForUpdateAsync(item.ProductId)
                ?? throw new OrderBusinessException($"商品“{product.ProductName}”缺少库存汇总");
            if (stock.AvailableQty < item.Quantity)
            {
                throw new OrderBusinessException(
                    $"商品“{product.ProductName}”库存不足，当前可用 {stock.AvailableQty}");
            }

            stock.LockedQty = checked(stock.LockedQty + item.Quantity);
            stock.AvailableQty = stock.TotalQty - stock.LockedQty;
            stock.UpdateTime = DateTime.Now;
            stockSummaryRepository.Update(stock);

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = product.ProductID,
                ProductName = product.ProductName,
                SupplierId = product.SupplierID,
                UnitPrice = product.DefaultPrice
            });
        }

        return snapshots;
    }

    public async Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        unitOfWork.AttachExternalTransaction(transaction);

        var items = request.Items
            .GroupBy(item => item.ProductId, StringComparer.Ordinal)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = checked(group.Sum(item => item.Quantity))
            })
            .OrderBy(item => item.ProductId, StringComparer.Ordinal)
            .ToList();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0)
                throw new OrderBusinessException("释放库存的商品数据无效");

            var stock = await stockSummaryRepository.GetByProductIdForUpdateAsync(item.ProductId)
                ?? throw new OrderBusinessException($"商品 {item.ProductId} 缺少库存汇总");
            if (stock.LockedQty < item.Quantity)
            {
                throw new OrderBusinessException(
                    $"商品 {item.ProductId} 的预留库存不足，无法释放订单 {request.OrderId}");
            }

            stock.LockedQty -= item.Quantity;
            stock.AvailableQty = stock.TotalQty - stock.LockedQty;
            stock.UpdateTime = DateTime.Now;
            stockSummaryRepository.Update(stock);
        }
    }
}
