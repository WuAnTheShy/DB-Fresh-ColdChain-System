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
}
