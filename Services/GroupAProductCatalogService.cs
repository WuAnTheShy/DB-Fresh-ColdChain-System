using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Services;

/// <summary>
/// 将 A 组公开商品与供应商服务适配为 B 组只读目录契约。
/// B 组不直接访问 A 组 Repository 或数据表。
/// </summary>
public sealed class GroupAProductCatalogService(
    IProductInventoryService productInventoryService,
    ISupplierService supplierService) : IGroupAProductCatalogService
{
    public async Task<GroupAProductSearchResult> SearchSellableProductsAsync(
        GroupAProductSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var products = await LoadSellableProductsAsync(cancellationToken);
        var supplierIds = request.SupplierIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var filtered = products
            .Where(product => supplierIds.Count == 0 || supplierIds.Contains(product.SupplierId))
            .Where(product => string.IsNullOrWhiteSpace(request.Keyword) ||
                product.ProductName.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(product => string.IsNullOrWhiteSpace(request.Category) ||
                string.Equals(product.CategoryName, request.Category.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => product.ProductName, StringComparer.Ordinal)
            .ThenBy(product => product.ProductId, StringComparer.Ordinal)
            .ToList();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        return new GroupAProductSearchResult
        {
            Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = filtered.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<GroupATrustedProduct>> GetTrustedProductsAsync(
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        var ids = productIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var products = await LoadSellableProductsAsync(cancellationToken);

        return products
            .Where(product => ids.Contains(product.ProductId))
            .Select(product => new GroupATrustedProduct
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                SupplierId = product.SupplierId,
                SalePrice = product.SalePrice,
                AvailableStock = product.AvailableStock,
                IsOnSale = product.IsInStock,
                CategoryName = product.CategoryName,
                Unit = product.Unit,
                StorageRequirement = product.StorageRequirement
            })
            .ToList();
    }

    private async Task<IReadOnlyList<GroupAConsumerProduct>> LoadSellableProductsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var supplierResponse = await supplierService.GetAllSuppliersAsync();
        if (!supplierResponse.IsSuccess || supplierResponse.Data == null)
            throw new GroupBBusinessException("商品供应商目录暂时不可用");

        var suppliersByName = supplierResponse.Data
            .Where(supplier => string.Equals(supplier.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Where(supplier => !string.IsNullOrWhiteSpace(supplier.SupplierName))
            .GroupBy(supplier => supplier.SupplierName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.SupplierID).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.First().SupplierID,
                StringComparer.OrdinalIgnoreCase);

        const int pageSize = 50;
        var pageIndex = 1;
        var result = new List<GroupAConsumerProduct>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await productInventoryService.GetProductsAsync(pageIndex, pageSize);
            if (!response.IsSuccess || response.Data == null)
                throw new GroupBBusinessException("商品目录暂时不可用");

            foreach (var product in response.Data.Items)
            {
                if (!string.Equals(product.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                    product.DefaultPrice <= 0 ||
                    string.IsNullOrWhiteSpace(product.SupplierName) ||
                    !suppliersByName.TryGetValue(product.SupplierName.Trim(), out var supplierId))
                {
                    continue;
                }

                result.Add(new GroupAConsumerProduct
                {
                    ProductId = product.ProductID,
                    ProductName = product.ProductName,
                    CategoryName = product.CategoryName,
                    Unit = product.Unit,
                    StorageRequirement = product.StorageReq,
                    SalePrice = product.DefaultPrice,
                    IsInStock = product.AvailableStock > 0,
                    AvailableStock = Math.Max(0, product.AvailableStock),
                    SupplierId = supplierId
                });
            }

            if (pageIndex >= response.Data.TotalPages) break;
            pageIndex++;
        }

        return result;
    }
}
