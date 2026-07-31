using Dapper;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public class ProductRepository : BaseRepository<Product>, IProductRepository
{
    public ProductRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<(List<Product> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var where = string.IsNullOrWhiteSpace(keyword)
            ? ""
            : """ WHERE p."NAME" LIKE '%' || :Keyword || '%' """;

        var countSql = $"""SELECT COUNT(*) FROM "PRODUCT" p {where}""";
        var dataSql = $"""
            SELECT p.*, s.*, i.*
            FROM "PRODUCT" p
            LEFT JOIN "SUPPLIER" s ON p."SUPPLIER_ID" = s."ID"
            LEFT JOIN "INVENTORY" i ON p."ID" = i."PRODUCT_ID"
            {where}
            ORDER BY p."ID"
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var dp = new DynamicParameters();
        dp.Add(":Keyword", keyword);
        dp.Add(":Skip", (pageIndex - 1) * pageSize);
        dp.Add(":Take", pageSize);

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, dp, _uow.Transaction);

        var items = await _uow.Connection.QueryAsync<Product, Models.Supplier, Inventory, Product>(
            dataSql,
            (prod, sup, inv) =>
            {
                prod.Supplier = sup;
                prod.Inventory = inv;
                return prod;
            },
            dp,
            _uow.Transaction,
            splitOn: "ID,ID"
        );

        return (items.ToList(), total);
    }

    public async Task<Product?> GetByIdWithDetailsAsync(int id)
    {
        var sql = """
            SELECT p.*, s.*, i.*
            FROM "PRODUCT" p
            LEFT JOIN "SUPPLIER" s ON p."SUPPLIER_ID" = s."ID"
            LEFT JOIN "INVENTORY" i ON p."ID" = i."PRODUCT_ID"
            WHERE p."ID" = :Id
            """;

        var result = await _uow.Connection.QueryAsync<Product, Models.Supplier, Inventory, Product>(
            sql,
            (prod, sup, inv) =>
            {
                prod.Supplier = sup;
                prod.Inventory = inv;
                return prod;
            },
            new { Id = id },
            _uow.Transaction,
            splitOn: "ID,ID"
        );

        return result.FirstOrDefault();
    }
}
