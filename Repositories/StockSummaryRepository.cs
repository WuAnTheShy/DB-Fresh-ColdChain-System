using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class StockSummaryRepository : BaseRepository<InvStockSummary>, IStockSummaryRepository
{
    public StockSummaryRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<InvStockSummary?> GetByProductIdAsync(string productId)
    {
        var sql = """SELECT * FROM Inv_StockSummary WHERE ProductID = :Id""";
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockSummary>(sql, new { Id = productId }, _uow.Transaction);
    }

    // 带行级锁查询 — SELECT ... FOR UPDATE，阻止并发修改
    // 必须在事务内调用，否则锁不生效
    public async Task<InvStockSummary?> GetByProductIdForUpdateAsync(string productId)
    {
        var sql = """SELECT * FROM Inv_StockSummary WHERE ProductID = :Id FOR UPDATE""";
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockSummary>(
            sql, new { Id = productId }, _uow.Transaction);
    }

    public async Task<List<InvStockSummary>> GetLowStockAsync(int threshold)
    {
        var sql = """
            SELECT st.*, p.*
            FROM Inv_StockSummary st
            LEFT JOIN Inv_Products p ON st.ProductID = p.ProductID
            WHERE st.TotalQty <= :Threshold
            ORDER BY st.TotalQty
            """;
        var result = await _uow.Connection.QueryAsync<InvStockSummary, InvProduct, InvStockSummary>(
            sql,
            (st, prod) => { st.Product = prod; return st; },
            new { Threshold = threshold }, _uow.Transaction, splitOn: "PRODUCTNAME");
        return result.ToList();
    }
}
