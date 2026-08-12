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
            ORDER BY ExpiryDate ASC NULLS LAST, ProductionDate ASC NULLS LAST, BatchNo ASC
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
            ORDER BY ExpiryDate ASC NULLS LAST, ProductionDate ASC NULLS LAST, BatchNo ASC
            FOR UPDATE SKIP LOCKED
            """;
        return (await _uow.Connection.QueryAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }

    /// <summary>查询批次含供应商信息</summary>
    public async Task<List<InvStockBatch>> GetByProductIdWithSupplierAsync(string productId)
    {
        var sql = """
            SELECT b.*, s.* FROM Inv_StockBatches b
            LEFT JOIN Inv_Suppliers s ON b.SupplierID = s.SupplierID
            WHERE b.ProductID = :Id AND b.CurrentQty > 0 AND b.Status = 'ACTIVE'
              AND (b.ExpiryDate IS NULL OR b.ExpiryDate >= SYSDATE)
            ORDER BY b.ExpiryDate ASC
            """;
        var result = await _uow.Connection.QueryAsync<InvStockBatch, InvSupplier, InvStockBatch>(sql,
            (batch, supplier) => { batch.Supplier = supplier; return batch; },
            new { Id = productId }, _uow.Transaction, splitOn: "SUPPLIERNAME");
        return result.ToList();
    }

    /// <summary>将已过期但仍为ACTIVE的批次标记为EXPIRED</summary>
    public async Task<int> MarkExpiredBatchesAsync()
    {
        var sql = """UPDATE Inv_StockBatches SET Status='EXPIRED' WHERE Status='ACTIVE' AND ExpiryDate IS NOT NULL AND ExpiryDate < SYSDATE""";
        return await _uow.Connection.ExecuteAsync(sql, transaction: _uow.Transaction);
    }

    /// <summary>查某产品活跃批次合计（过滤过期）</summary>
    public async Task<int> GetActiveTotalByProductIdAsync(string productId)
    {
        var sql = """SELECT NVL(SUM(CurrentQty),0) FROM Inv_StockBatches WHERE ProductID=:Id AND Status='ACTIVE' AND (ExpiryDate IS NULL OR ExpiryDate>=SYSDATE)""";
        return await _uow.Connection.ExecuteScalarAsync<int>(sql, new { Id = productId }, _uow.Transaction);
    }

    /// <summary>查前缀匹配的最大序号，用于自动生成批次号。如 BAT20260812 → 查当天已有批次的最大 NN</summary>
    public async Task<int> GetMaxBatchNoByPrefixAsync(string prefix)
    {
        var sql = """SELECT BatchNo FROM Inv_StockBatches WHERE BatchNo LIKE :Prefix """;
        var batchNos = await _uow.Connection.QueryAsync<string>(sql,
            new { Prefix = prefix + "%" }, _uow.Transaction);
        int max = 0;
        foreach (var bn in batchNos)
        {
            var dash = bn.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(bn[(dash + 1)..], out var n) && n > max)
                max = n;
        }
        return max;
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
            ORDER BY ExpiryDate ASC NULLS LAST, ProductionDate ASC NULLS LAST, BatchNo ASC
            FETCH FIRST 1 ROWS ONLY
            """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockBatch>(sql, new { Id = productId }, _uow.Transaction);
    }
}
