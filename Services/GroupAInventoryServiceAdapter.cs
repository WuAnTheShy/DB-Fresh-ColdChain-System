using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 将 A 组公开的物品、货物、库存和供应商服务适配为 B 组下单校验契约。
/// B 组不直接依赖 A 组 Repository，也不在下单阶段写入 A 组库存表。
/// </summary>
public sealed class GroupAInventoryServiceAdapter(
    IUnitOfWork unitOfWork,
    IProductInventoryService productInventoryService,
    IGoodsService goodsService,
    ISupplierService supplierService) : IGroupAInventoryGateway
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
        var goodsResponse = await goodsService.GetAllGoodsAsync();
        if (!goodsResponse.IsSuccess || goodsResponse.Data == null)
            throw new OrderBusinessException($"货物目录查询失败：{goodsResponse.Message}");
        var activeSupplierIds = await GetActiveSupplierIdsAsync(cancellationToken);
        var snapshots = new List<InventoryProductSnapshot>(normalized.Count);

        foreach (var item in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var productResponse = await productInventoryService.GetProductByIdAsync(item.ProductId);
            if (!productResponse.IsSuccess || productResponse.Data == null)
                throw new OrderBusinessException($"商品 {item.ProductId} 不存在或不可查询：{productResponse.Message}");

            var product = productResponse.Data;
            var sellableGoods = goodsResponse.Data
                .Where(goods => goods.ProductID == item.ProductId)
                .Where(goods => string.IsNullOrWhiteSpace(item.SupplierId) || goods.SupplierID == item.SupplierId)
                .Where(goods => string.Equals(goods.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                .Where(goods => goods.SalePrice > 0 && activeSupplierIds.Contains(goods.SupplierID))
                .GroupBy(goods => goods.SupplierID, StringComparer.Ordinal)
                .Select(group => group.Single())
                .ToList();
            if (sellableGoods.Count == 0)
                throw new OrderBusinessException($"商品“{product.ProductName}”没有可用的在售货物");
            if (sellableGoods.Count > 1)
                throw new OrderBusinessException($"商品“{product.ProductName}”存在多个在售供应商，当前结算请求缺少供应商标识");
            var goods = sellableGoods[0];

            var inventoryResponse = await productInventoryService.GetInventoryAsync(item.ProductId);
            if (!inventoryResponse.IsSuccess || inventoryResponse.Data == null)
                throw new OrderBusinessException($"商品“{product.ProductName}”缺少库存信息：{inventoryResponse.Message}");
            if (inventoryResponse.Data.AvailableQty < item.Quantity)
                throw new OrderBusinessException($"商品“{product.ProductName}”库存不足，当前可用 {inventoryResponse.Data.AvailableQty}");

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = product.ProductID,
                ProductName = product.ProductName,
                SupplierId = goods.SupplierID,
                UnitPrice = goods.SalePrice
            });
        }

        return snapshots;
    }

    private async Task<HashSet<string>> GetActiveSupplierIdsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await supplierService.GetAllSuppliersAsync();
        if (!response.IsSuccess || response.Data == null)
            throw new OrderBusinessException($"供应商目录查询失败：{response.Message}");
        return response.Data
            .Where(supplier => string.Equals(supplier.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Select(supplier => supplier.SupplierID)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<InventoryAvailabilityItem> Normalize(IReadOnlyList<InventoryAvailabilityItem> items)
    {
        try
        {
            return items
                .GroupBy(item => (ProductId: item.ProductId?.Trim() ?? string.Empty,
                    SupplierId: item.SupplierId?.Trim()), new ProductSupplierComparer())
                .Select(group => new InventoryAvailabilityItem
                {
                    ProductId = group.Key.ProductId,
                    SupplierId = group.Key.SupplierId,
                    Quantity = checked(group.Sum(item => item.Quantity))
                })
                .OrderBy(item => item.ProductId, StringComparer.Ordinal)
                .Select(item =>
                {
                    if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity is <= 0 or > 9999)
                        throw new OrderBusinessException("库存校验商品或数量无效");
                    return item;
                })
                .ToList();
        }
        catch (OverflowException)
        {
            throw new OrderBusinessException("商品数量超出允许范围");
        }
    }

    private sealed class ProductSupplierComparer : IEqualityComparer<(string ProductId, string? SupplierId)>
    {
        public bool Equals((string ProductId, string? SupplierId) x, (string ProductId, string? SupplierId) y) =>
            string.Equals(x.ProductId, y.ProductId, StringComparison.Ordinal) &&
            string.Equals(x.SupplierId, y.SupplierId, StringComparison.Ordinal);

        public int GetHashCode((string ProductId, string? SupplierId) obj) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.ProductId),
                obj.SupplierId == null ? 0 : StringComparer.Ordinal.GetHashCode(obj.SupplierId));
    }
}
