using Dapper;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public class ProductRepository : BaseRepository<InvProduct>, IProductRepository
{
    public ProductRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var where = string.IsNullOrWhiteSpace(keyword) ? ""
            : """ WHERE p."ProductName" LIKE '%' || :Keyword || '%' """;

        var countSql = $"""SELECT COUNT(*) FROM "Inv_Products" p {where}""";
        var dataSql = $"""
            SELECT p.*, c.*, s.*, st.*
            FROM "Inv_Products" p
            LEFT JOIN "Inv_Category" c ON p."CategoryID" = c."CategoryID"
            LEFT JOIN "Inv_Suppliers" s ON p."SupplierID" = s."SupplierID"
            LEFT JOIN "Inv_StockSummary" st ON p."ProductID" = st."ProductID"
            {where}
            ORDER BY p."ProductID"
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var dp = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(keyword)) dp.Add(":Keyword", keyword);
        dp.Add(":Skip", (pageIndex - 1) * pageSize);
        dp.Add(":Take", pageSize);

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, dp, _uow.Transaction);
        var items = await _uow.Connection.QueryAsync<InvProduct, InvCategory, InvSupplier, InvStockSummary, InvProduct>(
            dataSql,
            (prod, cat, sup, st) => { prod.Category = cat; prod.Supplier = sup; prod.StockSummary = st; return prod; },
            dp, _uow.Transaction, splitOn: "CategoryID,SupplierID,ProductID");

        return (items.ToList(), total);
    }

    public async Task<InvProduct?> GetByIdWithDetailsAsync(string id)
    {
        var sql = """
            SELECT p.*, c.*, s.*, st.*
            FROM "Inv_Products" p
            LEFT JOIN "Inv_Category" c ON p."CategoryID" = c."CategoryID"
            LEFT JOIN "Inv_Suppliers" s ON p."SupplierID" = s."SupplierID"
            LEFT JOIN "Inv_StockSummary" st ON p."ProductID" = st."ProductID"
            WHERE p."ProductID" = :Id
            """;
        var result = await _uow.Connection.QueryAsync<InvProduct, InvCategory, InvSupplier, InvStockSummary, InvProduct>(
            sql,
            (prod, cat, sup, st) => { prod.Category = cat; prod.Supplier = sup; prod.StockSummary = st; return prod; },
            new { Id = id }, _uow.Transaction, splitOn: "CategoryID,SupplierID,ProductID");
        return result.FirstOrDefault();
    }
}
