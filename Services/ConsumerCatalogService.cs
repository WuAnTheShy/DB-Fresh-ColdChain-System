using FreshColdChain.Interfaces;
using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>使用 A、C 组公开契约组装 B 组消费者端可售目录。</summary>
public sealed class ConsumerCatalogService(
    IGroupAProductCatalogService productCatalogService,
    IGroupCPromoterCatalogService promoterCatalogService) : IConsumerCatalogService
{
    public async Task<ConsumerCatalogResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var promoters = await promoterCatalogService.SearchAvailablePromotersAsync(
            new GroupCPromoterSearchRequest { Page = 1, PageSize = 50 },
            cancellationToken);
        var products = new List<ConsumerCatalogProduct>();

        foreach (var promoter in promoters.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var featuredProducts = await promoterCatalogService.GetFeaturedProductsAsync(
                promoter.PromoterId,
                cancellationToken);
            if (featuredProducts.Count == 0) continue;
            var trustedProducts = await productCatalogService.GetTrustedProductsAsync(
                featuredProducts.Select(item => item.ProductId).Distinct(StringComparer.Ordinal).ToList(),
                cancellationToken);
            // 可信商品按 ProductId 判定：同一商品只要仍在售且有库存，
            // 便允许团长将该商品来自不同供应商的多个上架分别展示。
            var trustedProductMap = trustedProducts
                .Where(item => item.IsOnSale && item.AvailableStock > 0)
                .GroupBy(item => item.ProductId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

            foreach (var featured in featuredProducts)
            {
                if (featured.SalePrice <= 0 ||
                    !trustedProductMap.TryGetValue(featured.ProductId, out var product))
                {
                    continue;
                }

                products.Add(new ConsumerCatalogProduct
                {
                    CatalogItemId = $"{promoter.PromoterId}:{featured.ProductId}:{featured.SupplierId}",
                    ProductId = featured.ProductId,
                    ProductName = product.ProductName,
                    CategoryName = string.IsNullOrWhiteSpace(product.CategoryName)
                        ? "其他"
                        : product.CategoryName.Trim(),
                    Unit = product.Unit,
                    StorageRequirement = product.StorageRequirement,
                    SupplierId = featured.SupplierId,
                    SupplierName = string.IsNullOrWhiteSpace(featured.SupplierName)
                        ? null
                        : featured.SupplierName.Trim(),
                    SalePrice = featured.SalePrice,
                    AvailableStock = product.AvailableStock,
                    PromoterId = promoter.PromoterId,
                    PublishedAt = featured.PublishedAt,
                    Description = featured.Description,
                    ImageUrls = featured.ImageUrls
                });
            }
        }

        var ordered = products
            .OrderBy(item => item.CategoryName, StringComparer.Ordinal)
            .ThenBy(item => item.ProductName, StringComparer.Ordinal)
            .ThenBy(item => item.PromoterId, StringComparer.Ordinal)
            .ThenBy(item => item.SupplierId, StringComparer.Ordinal)
            .ToList();
        return new ConsumerCatalogResult
        {
            Categories = ordered
                .Select(item => item.CategoryName)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Products = ordered
        };
    }
}
