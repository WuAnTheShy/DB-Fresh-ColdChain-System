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
            var trustedProductMap = trustedProducts.ToDictionary(
                item => $"{item.ProductId}\u001f{item.SupplierId}",
                StringComparer.Ordinal);

            foreach (var featured in featuredProducts)
            {
                var key = $"{featured.ProductId}\u001f{featured.SupplierId}";
                if (!trustedProductMap.TryGetValue(key, out var product) ||
                    !product.IsOnSale ||
                    product.AvailableStock <= 0 ||
                    featured.SalePrice <= 0)
                {
                    continue;
                }

                products.Add(new ConsumerCatalogProduct
                {
                    CatalogItemId = $"{promoter.PromoterId}:{product.ProductId}",
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    CategoryName = string.IsNullOrWhiteSpace(product.CategoryName)
                        ? "其他"
                        : product.CategoryName.Trim(),
                    Unit = product.Unit,
                    StorageRequirement = product.StorageRequirement,
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
