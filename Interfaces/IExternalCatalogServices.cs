using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// A 组提供的只读商品目录适配契约。
public interface IGroupAProductCatalogService
{
    Task<GroupAProductSearchResult> SearchSellableProductsAsync(
        GroupAProductSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupATrustedProduct>> GetTrustedProductsAsync(
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken = default);
}

// C 组提供的只读团长及合作范围适配契约。
public interface IGroupCPromoterCatalogService
{
    Task<GroupCPromoterSearchResult> SearchAvailablePromotersAsync(
        GroupCPromoterSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<GroupCPromoterSummary?> GetPromoterAsync(
        string promoterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCooperatingSupplierIdsAsync(
        string promoterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupCPromoterFeaturedProduct>> GetFeaturedProductsAsync(
        string promoterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupCPromoterProductValidation>>
        ValidatePromoterProductsAsync(
            string promoterId,
            IReadOnlyList<GroupCPromoterProductCandidate> products,
            CancellationToken cancellationToken = default);
}

// B 组消费者端商品目录聚合契约。
public interface IConsumerCatalogService
{
    Task<ConsumerCatalogResult> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}
