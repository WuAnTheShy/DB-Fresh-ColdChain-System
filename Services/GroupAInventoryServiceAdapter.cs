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
    IProductInventoryService productInventoryService,
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
        var suppliers = await GetActiveSuppliersAsync(cancellationToken);
        var snapshots = new List<InventoryProductSnapshot>(normalized.Count);

        foreach (var item in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var productResponse = await productInventoryService.GetProductByIdAsync(item.ProductId);
            if (!productResponse.IsSuccess || productResponse.Data == null)
                throw new OrderBusinessException($"商品 {item.ProductId} 不存在或不可查询：{productResponse.Message}");

            var product = productResponse.Data;
            if (!string.Equals(product.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                throw new OrderBusinessException($"商品“{product.ProductName}”已下架");
            if (product.DefaultPrice <= 0)
                throw new OrderBusinessException($"商品“{product.ProductName}”价格无效");

            var inventoryResponse = await productInventoryService.GetInventoryAsync(item.ProductId);
            if (!inventoryResponse.IsSuccess || inventoryResponse.Data == null)
                throw new OrderBusinessException($"商品“{product.ProductName}”缺少库存信息：{inventoryResponse.Message}");
            if (inventoryResponse.Data.AvailableQty < item.Quantity)
            {
                throw new OrderBusinessException(
                    $"商品“{product.ProductName}”库存不足，当前可用 {inventoryResponse.Data.AvailableQty}");
            }

            snapshots.Add(new InventoryProductSnapshot
            {
                ProductId = product.ProductID,
                ProductName = product.ProductName,
                SupplierId = ResolveSupplierId(product, suppliers),
                UnitPrice = product.DefaultPrice
            });
        }

        return snapshots;
    }

    private async Task<IReadOnlyList<SupplierDto>> GetActiveSuppliersAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await supplierService.GetAllSuppliersAsync();
        if (!response.IsSuccess || response.Data == null)
            throw new OrderBusinessException($"供应商目录查询失败：{response.Message}");

        return response.Data
            .Where(supplier =>
                string.Equals(supplier.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string ResolveSupplierId(
        ProductDto product,
        IReadOnlyList<SupplierDto> suppliers)
    {
        if (string.IsNullOrWhiteSpace(product.SupplierName))
            throw new OrderBusinessException($"商品“{product.ProductName}”缺少供应商");

        var matches = suppliers
            .Where(supplier => string.Equals(
                supplier.SupplierName,
                product.SupplierName,
                StringComparison.OrdinalIgnoreCase))
            .Select(supplier => supplier.SupplierID)
            .Where(supplierId => !string.IsNullOrWhiteSpace(supplierId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new OrderBusinessException(
                $"商品“{product.ProductName}”的供应商未启用或不存在"),
            _ => throw new OrderBusinessException(
                $"商品“{product.ProductName}”的供应商名称不唯一，无法安全拆单")
        };
    }

    private static IReadOnlyList<InventoryAvailabilityItem> Normalize(
        IReadOnlyList<InventoryAvailabilityItem> items)
    {
        try
        {
            return items
                .GroupBy(item => item.ProductId?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .Select(group => new InventoryAvailabilityItem
                {
                    ProductId = group.Key,
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
}
