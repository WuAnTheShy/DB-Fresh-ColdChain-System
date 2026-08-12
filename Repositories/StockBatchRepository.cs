using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class StockBatchRepository : BaseRepository<InvStockBatch>, IStockBatchRepository
{
    public StockBatchRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<InvStockBatch>> GetByProductIdAsync(string productId)
    {
        var sql = """
            SELECT * FROM Inv_StockBatches
            WHERE ProductID = :Id AND CurrentQty > 0 AND Status = 'ACTIVE'
              AND (ExpiryDate IS NULL OR ExpiryDate >= SYSDATE)
            ORDER BY ExpiryDate ASC
            """;
        return (await _uow.Connection.QueryAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }

    /// <summary>
    /// 带行级锁的 FEFO 批次查询：FOR UPDATE SKIP LOCKED
    /// 必须在事务内调用。并发出库时，已被其他事务锁定的批次行会被跳过，避免等待和死锁。
    /// </summary>
    public async Task<List<InvStockBatch>> GetByProductIdForUpdateAsync(string productId)
    {
        var sql = """
            SELECT * FROM Inv_StockBatches
            WHERE ProductID = :Id AND CurrentQty > 0 AND Status = 'ACTIVE'
              AND (ExpiryDate IS NULL OR ExpiryDate >= SYSDATE)
            ORDER BY ExpiryDate ASC
            FOR UPDATE SKIP LOCKED
            """;
        return (await _uow.Connection.QueryAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }

    /// <summary>查前缀匹配的最大序号，用于自动生成批次号。如 BAT20260812 → 查询 BAT20260812-% 的最大 NN</summary>
    public async Task<int> GetMaxBatchNoByPrefixAsync(string prefix)
    {
        var sql = """SELECT NVL(MAX(TO_NUMBER(REPLACE(BatchNo, :PrefixDash, ''))), 0) FROM Inv_StockBatches WHERE BatchNo LIKE :LikePrefix """;
        return await _uow.Connection.ExecuteScalarAsync<int>(sql,
            new { PrefixDash = prefix + "-", LikePrefix = prefix + "%" }, _uow.Transaction);
    }

    /// <summary>
    /// FEFO：取最早过期且还有库存的批次
    /// </summary>
    public async Task<InvStockBatch?> GetOldestAvailableBatchAsync(string productId)
    {
        var sql = """
            SELECT * FROM Inv_StockBatches
            WHERE ProductID = :Id AND CurrentQty > 0 AND Status = 'ACTIVE'
              AND (ExpiryDate IS NULL OR ExpiryDate >= SYSDATE)
            ORDER BY ExpiryDate ASC
            FETCH FIRST 1 ROWS ONLY
            """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction);
    }
}
