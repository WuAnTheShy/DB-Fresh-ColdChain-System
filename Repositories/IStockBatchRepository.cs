using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IStockBatchRepository : IBaseRepository<InvStockBatch>
{
    Task<List<InvStockBatch>> GetByProductIdAsync(string productId);
    Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId);
}
