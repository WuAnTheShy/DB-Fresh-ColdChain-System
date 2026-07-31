using Dapper;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Supplier;

public class SupplierRepository : BaseRepository<Models.Supplier>, ISupplierRepository
{
    public SupplierRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<Product>> GetProductsBySupplierIdAsync(int supplierId)
    {
        var sql = """
            SELECT p.*, i.*
            FROM "PRODUCT" p
            LEFT JOIN "INVENTORY" i ON p."ID" = i."PRODUCT_ID"
            WHERE p."SUPPLIER_ID" = :SupplierId
            """;
        var result = await _uow.Connection.QueryAsync<Product, Inventory, Product>(
            sql,
            (prod, inv) => { prod.Inventory = inv; return prod; },
            new { SupplierId = supplierId },
            _uow.Transaction,
            splitOn: "ID"
        );
        return result.ToList();
    }

    public async Task<(List<Models.Supplier> Items, int Total)> GetPagedWithProductCountAsync(int pageIndex, int pageSize)
    {
        var countSql = """SELECT COUNT(*) FROM "SUPPLIER" """;
        var dataSql = $"""
            SELECT s.*, COUNT(p."ID") AS ProductCount
            FROM "SUPPLIER" s
            LEFT JOIN "PRODUCT" p ON s."ID" = p."SUPPLIER_ID"
            GROUP BY s."ID", s."NAME", s."CONTACT_PERSON", s."PHONE", s."ADDRESS", s."REMARK", s."CREATE_TIME"
            ORDER BY s."ID"
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, transaction: _uow.Transaction);

        var items = await _uow.Connection.QueryAsync<Models.Supplier>(dataSql,
            new { Skip = (pageIndex - 1) * pageSize, Take = pageSize },
            _uow.Transaction);

        return (items.ToList(), total);
    }

    public async Task<Models.Supplier?> GetByIdWithProductsAsync(int id)
    {
        // 先查供应商，再查产品
        var supplier = await GetByIdAsync(id);
        if (supplier != null)
        {
            supplier.Products = await GetProductsBySupplierIdAsync(id);
        }
        return supplier;
    }
}
