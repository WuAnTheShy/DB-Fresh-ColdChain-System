using Dapper;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public class StockBatchRepository : BaseRepository<InvStockBatch>, IStockBatchRepository
{
    public StockBatchRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<InvStockBatch>> GetByProductIdAsync(string productId)
    {
        var sql = """
            SELECT * FROM "Inv_StockBatches"
            WHERE "ProductID" = :Id AND "CurrentQty" > 0 AND "Status" = 'ACTIVE'
            ORDER BY "ExpiryDate" ASC
            """;
        return (await _uow.Connection.QueryAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }

    /// <summary>
    /// FEFO：取最早过期且还有库存的批次
    /// </summary>
    public async Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId)
    {
        var sql = """
            SELECT * FROM "Inv_StockBatches"
            WHERE "ProductID" = :Id AND "CurrentQty" > 0 AND "Status" = 'ACTIVE'
            ORDER BY "ExpiryDate" ASC
            FETCH FIRST 1 ROWS ONLY
            """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction);
    }
}
