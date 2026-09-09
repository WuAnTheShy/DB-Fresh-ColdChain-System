namespace FreshColdChain.Interfaces;

/// <summary>
/// 供应商动态定价规则变更后的“已上架条目快照”同步服务。
/// 供应商新建/编辑/删除某商品的价格规则后，以最新规则引擎结果重算
/// 该（供应商×商品）名下所有团长已上架条目（CRM_PRODUCT_ENTRIES）的
/// SUPPLYPRICE（供应商动态定价）/ DEFAULTPRICE（推荐价）快照；
/// 团长定价超出新允许范围时自动钳制到范围内（落库后团长端/消费者端立即生效）。
/// </summary>
public interface IPromoterListedPriceSyncService
{
    /// <summary>
    /// 同步重算某供应商×商品下全部团长已上架条目的动态定价快照。
    /// 引擎计算失败的条目跳过（保留旧快照）；返回实际被刷新的条目数。
    /// </summary>
    Task<int> SyncListedPricesAsync(string supplierId, string productId);
}
