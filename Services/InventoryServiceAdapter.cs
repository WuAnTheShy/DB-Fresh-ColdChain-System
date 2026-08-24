using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 库存适配器 — 实现 B 组的 IInventoryService 接口
/// 内部调用 A 组自己的 Repository，使用 B 组传入的事务
/// </summary>
public class InventoryServiceAdapter : IInventoryService
{
    private readonly IStockSummaryRepository _stockRepo;
    private readonly IProductRepository _productRepo;
    private readonly IUnitOfWork _uow;

    public InventoryServiceAdapter(
        IStockSummaryRepository stockRepo,
        IProductRepository productRepo,
        IUnitOfWork uow)
    {
        _stockRepo = stockRepo;
        _productRepo = productRepo;
        _uow = uow;
    }

    /// <summary>
    /// 预留库存：FOR UPDATE 行级锁 → 校验 → 增加 LockedQty → 返回商品快照
    /// 全部使用 B 组传入的事务
    /// </summary>
    public async Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transaction);

        // 挂载 B 组的事务
        _uow.AttachExternalTransaction(transaction);

        var snapshots = new List<InventoryProductSnapshot>(items.Count);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // FOR UPDATE 锁库存行（走 B 组事务的连接）
            var stock = await _stockRepo.GetByProductIdForUpdateAsync(item.ProductId);
            if (stock == null)
                throw new InvalidOperationException($"商品 {item.ProductId} 库存记录不存在");

            // 防超卖校验
            if (stock.AvailableQty < item.Quantity)
                throw new InvalidOperationException(
                    $"商品 {item.ProductId} 库存不足：需要 {item.Quantity}，可用 {stock.AvailableQty}");

            // 锁定库存
            stock.LockedQty += item.Quantity;
            stock.AvailableQty = stock.TotalQty - stock.LockedQty;
            stock.UpdateTime = DateTime.Now;
            _stockRepo.Update(stock);

            // 查商品信息，组装可信快照
            var product = await _productRepo.GetByIdAsync(item.ProductId);
            if (product == null)
                throw new InvalidOperationException($"商品 {item.ProductId} 不存在");

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = product.ProductID,
                ProductName = product.ProductName,
                SupplierId = product.SupplierID ?? string.Empty,
                UnitPrice = product.DefaultPrice
            });
        }

        return snapshots;
    }

    /// <summary>
    /// 释放库存：将 LockedQty 恢复为可用
    /// </summary>
    public async Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);

        _uow.AttachExternalTransaction(transaction);

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stock = await _stockRepo.GetByProductIdForUpdateAsync(item.ProductId);
            if (stock == null) continue; // 幂等：库存记录不存在则跳过

            stock.LockedQty = Math.Max(0, stock.LockedQty - item.Quantity);
            stock.AvailableQty = stock.TotalQty - stock.LockedQty;
            stock.UpdateTime = DateTime.Now;
            _stockRepo.Update(stock);
        }
    }
}
