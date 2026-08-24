using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 商品目录适配器 — 实现 B 组的 IGroupAProductCatalogService 接口
/// 复用 A 组 ProductRepository，只读查询不参与事务。
/// </summary>
public class ProductCatalogServiceAdapter : IGroupAProductCatalogService
{
    private readonly IProductRepository _productRepo;

    public ProductCatalogServiceAdapter(IProductRepository productRepo)
    {
        _productRepo = productRepo;
    }

    /// <summary>按供应商集合查询可售商品（消费者可见信息，供应商编号由 [JsonIgnore] 拦截）</summary>
    public async Task<GroupAProductSearchResult> SearchSellableProductsAsync(
        GroupAProductSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        cancellationToken.ThrowIfCancellationRequested();

        var (items, total) = await _productRepo.GetPagedBySuppliersAsync(
            request.SupplierIds, request.Keyword, request.Category, page, pageSize);

        return new GroupAProductSearchResult
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(MapToConsumer).ToList()
        };
    }

    /// <summary>批量查询商品可信信息（含内部供应商编号，供结算/下单校验）</summary>
    public async Task<IReadOnlyList<GroupATrustedProduct>> GetTrustedProductsAsync(
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        cancellationToken.ThrowIfCancellationRequested();

        var products = await _productRepo.GetByIdsWithDetailsAsync(productIds);
        return products.Select(MapToTrusted).ToList();
    }

    private static GroupAConsumerProduct MapToConsumer(InvProduct p)
    {
        var available = p.StockSummary?.AvailableQty ?? 0;
        return new GroupAConsumerProduct
        {
            ProductId = p.ProductID,
            ProductName = p.ProductName,
            ImageUrl = null, // 图片列尚未建立，暂返回空占位
            SalePrice = p.DefaultPrice,
            IsInStock = available > 0,
            SupplierId = p.SupplierID ?? string.Empty
        };
    }

    private static GroupATrustedProduct MapToTrusted(InvProduct p) => new()
    {
        ProductId = p.ProductID,
        ProductName = p.ProductName,
        SupplierId = p.SupplierID ?? string.Empty,
        SalePrice = p.DefaultPrice,
        AvailableStock = p.StockSummary?.AvailableQty ?? 0,
        IsOnSale = p.Status == "ACTIVE"
    };
}
