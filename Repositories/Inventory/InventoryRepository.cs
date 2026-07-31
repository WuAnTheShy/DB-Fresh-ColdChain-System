using Dapper;
using FreshGroupSystem.Data;
using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public class InventoryRepository : BaseRepository<Inventory>, IInventoryRepository
{
    public InventoryRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<Inventory?> GetByProductIdAsync(int productId)
    {
        var sql = """
            SELECT i.*, p.*
            FROM "INVENTORY" i
            LEFT JOIN "PRODUCT" p ON i."PRODUCT_ID" = p."ID"
            WHERE i."PRODUCT_ID" = :ProductId
            """;
        var result = await _uow.Connection.QueryAsync<Inventory, Product, Inventory>(
            sql,
            (inv, prod) => { inv.Product = prod; return inv; },
            new { ProductId = productId },
            _uow.Transaction,
            splitOn: "ID"
        );
        return result.FirstOrDefault();
    }

    public async Task<List<Inventory>> GetLowStockAsync(int threshold)
    {
        var sql = """
            SELECT i.*, p.*
            FROM "INVENTORY" i
            LEFT JOIN "PRODUCT" p ON i."PRODUCT_ID" = p."ID"
            WHERE i."STOCK_QUANTITY" <= :Threshold
            ORDER BY i."STOCK_QUANTITY"
            """;
        var result = await _uow.Connection.QueryAsync<Inventory, Product, Inventory>(
            sql,
            (inv, prod) => { inv.Product = prod; return inv; },
            new { Threshold = threshold },
            _uow.Transaction,
            splitOn: "ID"
        );
        return result.ToList();
    }
}
