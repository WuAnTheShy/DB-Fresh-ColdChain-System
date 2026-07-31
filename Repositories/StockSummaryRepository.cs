using Dapper;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public class StockSummaryRepository : BaseRepository<InvStockSummary>, IStockSummaryRepository
{
    public StockSummaryRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<InvStockSummary?> GetByProductIdAsync(string productId)
    {
        var sql = """SELECT * FROM "Inv_StockSummary" WHERE "ProductID" = :Id""";
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvStockSummary>(sql, new { Id = productId }, _uow.Transaction);
    }

    public async Task<List<InvStockSummary>> GetLowStockAsync(int threshold)
    {
        var sql = """
            SELECT st.*, p.*
            FROM "Inv_StockSummary" st
            LEFT JOIN "Inv_Products" p ON st."ProductID" = p."ProductID"
            WHERE st."TotalQty" <= :Threshold
            ORDER BY st."TotalQty"
            """;
        var result = await _uow.Connection.QueryAsync<InvStockSummary, InvProduct, InvStockSummary>(
            sql,
            (st, prod) => { st.Product = prod; return st; },
            new { Threshold = threshold }, _uow.Transaction, splitOn: "ProductID");
        return result.ToList();
    }
}
