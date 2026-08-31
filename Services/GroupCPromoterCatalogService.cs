using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Services;

/// <summary>C 组团长目录、合作供应商和带货商品关系的正式只读适配。</summary>
public sealed class GroupCPromoterCatalogService(
    PromoterService promoterService) : IGroupCPromoterCatalogService
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
        var supplierIds = promoterEnabled
            ? (await promoterService.GetActiveSupplierIdsAsync(promoterId))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var entries = promoterEnabled
            ? await promoterService.GetPromoterFeaturedProductsAsync(promoterId)
            : [];
        var entryMap = entries.ToDictionary(
            entry => $"{entry.ProductID}\u001f{entry.SupplierID}",
            entry => entry.Price,
            StringComparer.Ordinal);

        return products.Select(product =>
        {
            var key = $"{product.ProductId}\u001f{product.SupplierId}";
            var hasEntry = entryMap.TryGetValue(key, out var promoterPrice);
            return new GroupCPromoterProductValidation
            {
                ProductId = product.ProductId,
                IsAllowed = promoterEnabled &&
                    supplierIds.Contains(product.SupplierId) &&
                    hasEntry,
                SalePrice = hasEntry ? promoterPrice : null
            };
        }).ToList();
    }

    private static bool IsEnabled(string? status) =>
        status is not null &&
        (status.Equals("ENABLE", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("ENABLED", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase));
}
