//操作InvStockBatch表

using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IStockBatchRepository : IBaseRepository<InvStockBatch>
{
    Task<List<InvStockBatch>> GetByProductIdAsync(string productId);//查询某个商品的所有库存批次
    Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId);//查询某个商品的最早可用库存批次（按入库时间升序排序，且库存数量大于 0）
}
