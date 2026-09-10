using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Services;

// C 组团长目录、合作供应商和带货商品关系的正式只读适配。
public sealed class GroupCPromoterCatalogService(
    PromoterService promoterService,
    PromoterIntroStore introStore) : IGroupCPromoterCatalogService
{
    public async Task<GroupCPromoterSearchResult> SearchAvailablePromotersAsync(
        GroupCPromoterSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var result = await promoterService.GetAvailablePromotersAsync(
            new GroupC_AvailablePromoterQuery
            {
                Keyword = request.Keyword,
                PageIndex = page,
                PageSize = pageSize
            },
            cancellationToken);

        return new GroupCPromoterSearchResult
        {
            Items = result.Items.Select(item => new GroupCPromoterSummary
            {
                PromoterId = item.PromoterId,
                PromoterName = item.PromoterName,
                AccountStatus = "ACTIVE"
            }).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<GroupCPromoterSummary?> GetPromoterAsync(
        string promoterId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var promoter = await promoterService.GetPromoterBasicInfoAsync(
            promoterId,
            cancellationToken);
        return promoter == null
            ? null
            : new GroupCPromoterSummary
            {
                PromoterId = promoter.PromoterId,
                PromoterName = promoter.PromoterName,
                AccountStatus = promoter.Status
            };
    }

    public async Task<IReadOnlyList<string>> GetCooperatingSupplierIdsAsync(
        string promoterId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await promoterService.GetActiveSupplierIdsAsync(promoterId);
    }

    public async Task<IReadOnlyList<GroupCPromoterFeaturedProduct>> GetFeaturedProductsAsync(
        string promoterId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await promoterService.GetPromoterFeaturedProductsAsync(promoterId);
        var result = new List<GroupCPromoterFeaturedProduct>(entries.Count);
        foreach (var entry in entries)
        {
            // PROMOTERDESC 现为“图文内容相对路径”或历史纯文字：为跨组消费者目录提供纯文本简介
            var description = await introStore.ToPlainTextAsync(entry.PromoterDesc, 2000);
            result.Add(new GroupCPromoterFeaturedProduct
            {
                ProductId = entry.ProductID,
                SupplierId = entry.SupplierID,
                SupplierName = entry.SupplierName,
                SalePrice = entry.Price,
                PublishedAt = entry.PublishedAt,
                Description = description,
                ImageUrls = entry.Images
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<GroupCPromoterProductValidation>> ValidatePromoterProductsAsync(
        string promoterId,
        IReadOnlyList<GroupCPromoterProductCandidate> products,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var promoter = await promoterService.GetPromoterBasicInfoAsync(
            promoterId,
            cancellationToken);
        var promoterEnabled = promoter != null && IsEnabled(promoter.Status);
        var entries = promoterEnabled
            ? await promoterService.GetPromoterFeaturedProductsAsync(promoterId)
            : [];
        var entryMap = entries
            .GroupBy(entry => $"{entry.ProductID}\u001f{entry.SupplierID}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<GroupCPromoterProductValidation>(products.Count);
        foreach (var product in products)
        {
            var key = $"{product.ProductId}\u001f{product.SupplierId}";
            var hasEntry = entryMap.TryGetValue(key, out var entry);
            string? description = null;
            if (hasEntry)
            {
                // 详情存储值可能是图文文件相对路径或历史纯文字，跨组目录一律取纯文本简介
                description = await introStore.ToPlainTextAsync(entry!.PromoterDesc, 2000);
            }
            result.Add(new GroupCPromoterProductValidation
            {
                ProductId = product.ProductId,
                SupplierId = product.SupplierId,
                // 授权以「已入团且在售」为准；不再额外要求 CRM_PSRELATION，
                // 因为团长上架货盘时往往只写了 CRM_PRODUCT_ENTRIES。
                IsAllowed = promoterEnabled && hasEntry,
                SalePrice = hasEntry ? entry!.Price : null,
                Description = description,
                ImageUrls = hasEntry ? entry!.Images : []
            });
        }
        return result;
    }

    private static bool IsEnabled(string? status) =>
        status is not null &&
        (status.Equals("ENABLE", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("ENABLED", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase));
}
