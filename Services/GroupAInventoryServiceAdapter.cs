using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// 将 A 组公开的商品、库存和供应商服务适配为 B 组下单校验契约。
// B 组不直接依赖 A 组 Repository，也不在下单阶段写入 A 组库存表。
public sealed class GroupAInventoryServiceAdapter(
    IUnitOfWork unitOfWork,
    IProductInventoryService productInventoryService,
    ISupplierService supplierService) : IGroupAInventoryGateway
{
    // 禁止销售的供应商账户状态：待审核（Pending）供应商的商品是否可售由 A 组货物状态决定，
    // B 组只拦截已被禁用或资质审核驳回的供应商，避免越权收紧 A 组的上架规则。
    private static readonly string[] BlockedSupplierStatuses = ["Disabled", "Rejected"];

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
        // 同一批次的同一供应商只向 A 组供应商服务校验一次
        var validatedSuppliers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (validatedSuppliers.Add(item.SupplierId))
            {
                await EnsureSupplierSellableAsync(item.SupplierId, cancellationToken);
            }

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

    // 通过 A 组公开的供应商服务确认交易对手可用：供应商不存在、已被禁用或审核驳回时禁止下单。
    private async Task EnsureSupplierSellableAsync(string supplierId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await supplierService.GetSupplierByIdAsync(supplierId);
        if (!response.IsSuccess || response.Data == null)
        {
            throw new OrderBusinessException($"供应商 {supplierId} 不存在或不可用：{response.Message}");
        }

        var status = response.Data.Status?.Trim();
        if (status != null &&
            BlockedSupplierStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            throw new OrderBusinessException(
                $"供应商“{response.Data.SupplierName}”当前状态为 {status}，无法销售其商品");
        }
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
