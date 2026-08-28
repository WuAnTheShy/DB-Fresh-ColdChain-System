using FreshColdChain.Models.DTOs;
using System.Data;

namespace FreshColdChain.Repositories;

/// <summary>
/// 商品入团表（CRM_PRODUCT_ENTRIES）仓库接口。
/// 记录（团长，商品，供应商）三元组，团长-商品为多对多关系。
/// </summary>
public interface IPromoterProductRepository
{
    /// <summary>
    /// 将（商品，供应商）加入/更新为团长入团商品（存在则改状态为 Active）。
    /// promoterPrice 为团长定价，null 表示未填写（入库前应由服务层回填为推荐价）。
    /// </summary>
    Task<bool> AddOrUpdateEntryAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice = null, string status = "Active", IDbTransaction? transaction = null);

    /// <summary>将（商品，供应商）从团长入团商品中移除（软删除为 Inactive）</summary>
    Task<bool> SoftDeleteEntryAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null);

    /// <summary>更新已入团（商品，供应商）组合的团长定价（仅限 Active 记录）</summary>
    Task<bool> UpdateEntryPriceAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice, IDbTransaction? transaction = null);

    /// <summary>查询团长已入团商品详情（含商品名、供应商名、报价、推荐价、团长定价）</summary>
    Task<List<PromoterProductEntryDetailDto>> GetActiveEntriesDetailAsync(string promoterId, IDbTransaction? transaction = null);

    /// <summary>查询团长当前所有已入团的（商品，供应商，团长定价）组合</summary>
    Task<List<(string ProductId, string SupplierId, decimal? PromoterPrice)>> GetActiveEntriesByPromoterAsync(string promoterId, IDbTransaction? transaction = null);

    /// <summary>判断（商品，供应商）是否已在团长入团商品中</summary>
    Task<bool> IsEntryActiveAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null);
}
