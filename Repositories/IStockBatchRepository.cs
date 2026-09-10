//操作InvStockBatch表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IStockBatchRepository : IBaseRepository<InvStockBatch>
{
    Task<List<InvStockBatch>> GetByProductIdAsync(string productId);//查询某个商品的所有库存批次（无锁，只读用）
    // 带行级锁的 FEFO 批次查询 — 出库扣减用。SKIP LOCKED 跳过已被其他事务锁定的行
    Task<List<InvStockBatch>> GetByProductIdForUpdateAsync(string productId);
    Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId);//查询某个商品的最早可用库存批次（按入库时间升序排序，且库存数量大于 0）
    // 查前缀匹配的最大序号，用于自动生成批次号
    Task<int> GetMaxBatchNoByPrefixAsync(string prefix);
    // 查询批次含供应商信息
    Task<List<InvStockBatch>> GetByProductIdWithSupplierAsync(string productId);
    // 查询某商品在某供应商下的全部批次（含供应商信息，不做过期/数量过滤，供供应商查看自己进货）
    Task<List<InvStockBatch>> GetByProductAndSupplierWithSupplierAsync(string productId, string supplierId);
    // 带行级锁的 FEFO 批次查询 — 限定 (商品, 供应商)。出库/发货按供应商维度扣减时使用
    Task<List<InvStockBatch>> GetByProductAndSupplierForUpdateAsync(string productId, string supplierId);
    // 查某商品在某供应商下的活跃批次合计（未过期）— 供应商级可用量口径
    Task<int> GetActiveTotalByProductAndSupplierAsync(string productId, string supplierId);
    // 将已过期但仍为ACTIVE的批次标记为EXPIRED,返回更新行数
    Task<int> MarkExpiredBatchesAsync();
    // 查某产品活跃批次合计
    Task<int> GetActiveTotalByProductIdAsync(string productId);
}
