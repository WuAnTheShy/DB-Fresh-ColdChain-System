using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 将 A 组公开的商品、库存和供应商服务适配为 B 组下单校验契约。
/// B 组不直接依赖 A 组 Repository，也不在下单阶段写入 A 组库存表。
/// </summary>
public sealed class GroupAInventoryServiceAdapter(
    IUnitOfWork unitOfWork,
    IProductInventoryService productInventoryService) : IGroupAInventoryGateway
{
    public async Task<IReadOnlyList<InventoryProductSnapshot>> CheckAvailabilityAsync(
        IReadOnlyList<InventoryAvailabilityItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transaction);
        if (items.Count == 0)
            throw new OrderBusinessException("库存校验商品不能为空");

        unitOfWork.AttachExternalTransaction(transaction);
        var normalized = Normalize(items);
        var snapshots = new List<InventoryProductSnapshot>(normalized.Count);

        foreach (var item in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 交易身份 = (商品, 供应商)：同一商品不同供应商各自校验货物、售价与该供应商可用库存
            var goodsResponse = await productInventoryService.GetSupplierGoodsInventoryAsync(
                item.ProductId,
                item.SupplierId);
            if (!goodsResponse.IsSuccess || goodsResponse.Data == null)
                throw new OrderBusinessException(
                    $"商品 {item.ProductId}（供应商 {item.SupplierId}）不可销售：{goodsResponse.Message}");

            var goods = goodsResponse.Data;
            if (!string.Equals(goods.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                throw new OrderBusinessException($"商品“{goods.ProductName}”已下架");
            if (goods.SalePrice <= 0)
                throw new OrderBusinessException($"商品“{goods.ProductName}”价格无效");
            if (goods.AvailableQty < item.Quantity)
            {
                throw new OrderBusinessException(
                    $"商品“{goods.ProductName}”库存不足，当前可用 {goods.AvailableQty}，需要 {item.Quantity}");
            }

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = goods.ProductID,
                ProductName = goods.ProductName,
                SupplierId = goods.SupplierID,
                UnitPrice = goods.SalePrice
            });
        }

        return snapshots;
    }

    private static IReadOnlyList<InventoryAvailabilityItem> Normalize(
        IReadOnlyList<InventoryAvailabilityItem> items)
    {
        try
        {
            return items
                .GroupBy(item => (
                    ProductId: item.ProductId?.Trim() ?? string.Empty,
                    SupplierId: item.SupplierId?.Trim() ?? string.Empty))
                .Select(group => new InventoryAvailabilityItem
                {
                    ProductId = group.Key.ProductId,
                    SupplierId = group.Key.SupplierId,
                    Quantity = checked(group.Sum(item => item.Quantity))
                })
                .OrderBy(item => item.ProductId, StringComparer.Ordinal)
                .ThenBy(item => item.SupplierId, StringComparer.Ordinal)
                .Select(item =>
                {
                    if (string.IsNullOrWhiteSpace(item.ProductId) ||
                        string.IsNullOrWhiteSpace(item.SupplierId) ||
                        item.Quantity is <= 0 or > 9999)
                        throw new OrderBusinessException("库存校验商品、供应商或数量无效");
                    return item;
                })
                .ToList();
        }
        catch (OverflowException)
        {
            throw new OrderBusinessException("商品数量超出允许范围");
        }
    }
}
