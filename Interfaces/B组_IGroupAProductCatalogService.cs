using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// A 组提供的只读商品目录适配契约（与 B 组 IExternalCatalogServices 中定义一致）。
/// 只读查询不参与事务。
/// </summary>
public interface IGroupAProductCatalogService
{
    /// <summary>
    /// 按供应商集合查询可售商品。
    /// B 组先从 C 组取得团长合作供应商集合，再传给 A 组查询可售商品。
    /// 返回消费者可见信息，内部供应商编号由 [JsonIgnore] 禁止输出给消费者前端。
    /// </summary>
    Task<GroupAProductSearchResult> SearchSellableProductsAsync(
        GroupAProductSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量查询商品可信信息。
    /// 结算和下单前重新读取实际价格、库存、上下架状态与供应商，禁止信任前端传入。
    /// </summary>
    Task<IReadOnlyList<GroupATrustedProduct>> GetTrustedProductsAsync(
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken = default);
}
