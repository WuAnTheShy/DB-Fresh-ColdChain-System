//操作InvStockBatch表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IStockBatchRepository : IBaseRepository<InvStockBatch>
{
    Task<List<InvStockBatch>> GetByProductIdAsync(string productId);//查询某个商品的所有库存批次（无锁，只读用）
    /// <summary>带行级锁的 FEFO 批次查询 — 出库扣减用。SKIP LOCKED 跳过已被其他事务锁定的行</summary>
    Task<List<InvStockBatch>> GetByProductIdForUpdateAsync(string productId);
    Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId);//查询某个商品的最早可用库存批次（按入库时间升序排序，且库存数量大于 0）
    /// <summary>查前缀匹配的最大序号，用于自动生成批次号</summary>
    Task<int> GetMaxBatchNoByPrefixAsync(string prefix);
    /// <summary>查询批次含供应商信息</summary>
    Task<List<InvStockBatch>> GetByProductIdWithSupplierAsync(string productId);
    /// <summary>查询某商品在某供应商下的全部批次（含供应商信息，不做过期/数量过滤，供供应商查看自己进货）</summary>
    Task<List<InvStockBatch>> GetByProductAndSupplierWithSupplierAsync(string productId, string supplierId);
    /// <summary>将已过期但仍为ACTIVE的批次标记为EXPIRED,返回更新行数</summary>
    Task<int> MarkExpiredBatchesAsync();
    /// <summary>查某产品活跃批次合计</summary>
    Task<int> GetActiveTotalByProductIdAsync(string productId);
}
