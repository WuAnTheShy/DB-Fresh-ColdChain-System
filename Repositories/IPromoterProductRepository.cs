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
    /// supplyPrice / defaultPrice 为入团时快照的“供应商动态报价”与“推荐价（=报价×1.2）”，
    /// 为 null 时 INSERT 不写值 / UPDATE 保留原快照（历史兼容）。
    /// promoterDesc 为团长带货介绍，入团时默认复制供应商商品文字；重复入团且传入 null 时保留原团长文字。
    /// </summary>
    Task<bool> AddOrUpdateEntryAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice = null, decimal? supplyPrice = null, decimal? defaultPrice = null, string? promoterDesc = null, string status = "Active", IDbTransaction? transaction = null);

    /// <summary>将（商品，供应商）从团长入团商品中移除（软删除为 Inactive）</summary>
    Task<bool> SoftDeleteEntryAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null);

    /// <summary>更新已入团（商品，供应商）组合的团长定价（仅限 Active 记录）</summary>
    Task<bool> UpdateEntryPriceAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice, IDbTransaction? transaction = null);

    /// <summary>更新已入团（商品，供应商）组合的团长带货介绍文字（仅限 Active 记录）</summary>
    Task<bool> UpdateEntryDescriptionAsync(string promoterId, string productId, string supplierId, string? promoterDesc, IDbTransaction? transaction = null);

    /// <summary>查询团长已入团商品详情（含商品名、供应商名、报价、推荐价、团长定价）</summary>
    Task<List<PromoterProductEntryDetailDto>> GetActiveEntriesDetailAsync(string promoterId, IDbTransaction? transaction = null);

    /// <summary>查询指定（商品 × 供应商）入团组合的详情；不存在或非 Active 时返回 null</summary>
    Task<PromoterProductEntryDetailDto?> GetActiveEntryDetailAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null);

    /// <summary>查询团长当前所有已入团的（商品，供应商，团长定价）组合</summary>
    Task<List<(string ProductId, string SupplierId, decimal? PromoterPrice)>> GetActiveEntriesByPromoterAsync(string promoterId, IDbTransaction? transaction = null);

    /// <summary>判断（商品，供应商）是否已在团长入团商品中</summary>
    Task<bool> IsEntryActiveAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null);

    /// <summary>
    /// 查询某供应商×商品下所有团长已上架（Active）的入团记录
    /// （团长ID + 当前团长定价），供规则变更后同步重算动态定价快照使用。
    /// </summary>
    Task<List<(string PromoterId, decimal? PromoterPrice)>> GetActiveListedEntriesAsync(string supplierId, string productId, IDbTransaction? transaction = null);

    /// <summary>
    /// 刷新某（团长，商品，供应商）入团记录的动态定价快照：
    /// supplyPrice/defaultPrice 由调用方按最新规则引擎算出，promoterPrice 为钳制后的团长定价。
    /// 仅更新 Active 记录，返回受影响行数。
    /// </summary>
    Task<int> RefreshListedEntrySnapshotAsync(string promoterId, string productId, string supplierId, decimal supplyPrice, decimal defaultPrice, decimal promoterPrice, IDbTransaction? transaction = null);
}
