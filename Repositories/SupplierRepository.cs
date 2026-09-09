using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class SupplierRepository : BaseRepository<InvSupplier>, ISupplierRepository
{
    public SupplierRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<InvProduct>> GetProductsBySupplierIdAsync(string supplierId)
    {
        var sql = """
            SELECT p.*, s.*
            FROM Inv_Products p
            LEFT JOIN Inv_StockSummary s ON p.ProductID = s.ProductID
            WHERE p.SupplierID = :Id
            """;
        var result = await _uow.Connection.QueryAsync<InvProduct, InvStockSummary, InvProduct>(
            sql,
            (prod, summary) => { prod.StockSummary = summary; return prod; },
            new { Id = supplierId }, _uow.Transaction, splitOn: "STOCKID");
        return result.ToList();
    }

    public async Task<(List<InvSupplier> Items, int Total)> GetPagedWithProductCountAsync(int pageIndex, int pageSize)
    {
        var countSql = """SELECT COUNT(*) FROM Inv_Suppliers """;
        var dataSql = $"""
            SELECT s.*, COUNT(g.ProductID) AS ProductCount
            FROM Inv_Suppliers s
            LEFT JOIN Inv_Goods g ON s.SupplierID = g.SupplierID
            GROUP BY s.SupplierID, s.SupplierName, s.LicenseNo, s.ExpiryDate, s.CreditLevel, s.ContactPhone, s.LoginAccount, s.LoginPassword, s.Status
            ORDER BY s.SupplierID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;
        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, transaction: _uow.Transaction);
        var items = await _uow.Connection.QueryAsync<InvSupplier>(dataSql,
            new { Skip = (pageIndex - 1) * pageSize, Take = pageSize }, _uow.Transaction);
        return (items.ToList(), total);
    }

    public async Task<InvSupplier?> GetByIdWithProductsAsync(string id)
    {
        var supplier = await GetByIdAsync(id);
        if (supplier != null)
            supplier.Products = await GetProductsBySupplierIdAsync(id);
        return supplier;
    }
}
