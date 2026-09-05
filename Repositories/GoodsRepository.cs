using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 货物仓储（复合主码，直接使用 Dapper，不继承单主码的 BaseRepository）。
/// </summary>
public class GoodsRepository : IGoodsRepository
{
    private readonly IUnitOfWork _uow;

    public GoodsRepository(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private const string SelectWithDetails = """
        SELECT g.ProductID, g.SupplierID, g.SalePrice, g.Status, g.StorageReq, g.ShelfLifeHours,
               g.Description, g.CreateTime, g.UpdateTime,
               p.ProductName, s.SupplierName
        FROM Inv_Goods g
        JOIN Inv_Products p ON g.ProductID = p.ProductID
        JOIN Inv_Suppliers s ON g.SupplierID = s.SupplierID
        """;

    public async Task<List<InvGoods>> GetBySupplierAsync(string supplierId)
    {
        var sql = SelectWithDetails + " WHERE g.SupplierID = :SupplierId ORDER BY p.ProductName";
        var result = await _uow.Connection.QueryAsync<InvGoods, InvProduct, InvSupplier, InvGoods>(sql,
            (goods, product, supplier) => { goods.Product = product; goods.Supplier = supplier; return goods; },
            new { SupplierId = supplierId }, _uow.Transaction, splitOn: "PRODUCTNAME,SUPPLIERNAME");
        return result.ToList();
    }

    public async Task<List<InvGoods>> GetAllAsync(string? keyword = null)
    {
        var where = string.IsNullOrWhiteSpace(keyword)
            ? ""
            : " WHERE p.ProductName LIKE '%' || :Keyword || '%' OR s.SupplierName LIKE '%' || :Keyword || '%'";
        var sql = SelectWithDetails + where + " ORDER BY p.ProductName, s.SupplierName";
        var result = await _uow.Connection.QueryAsync<InvGoods, InvProduct, InvSupplier, InvGoods>(sql,
            (goods, product, supplier) => { goods.Product = product; goods.Supplier = supplier; return goods; },
            new { Keyword = keyword }, _uow.Transaction, splitOn: "PRODUCTNAME,SUPPLIERNAME");
        return result.ToList();
    }

    public async Task<InvGoods?> GetAsync(string productId, string supplierId)
    {
        var sql = SelectWithDetails + " WHERE g.ProductID = :ProductId AND g.SupplierID = :SupplierId";
        var result = await _uow.Connection.QueryAsync<InvGoods, InvProduct, InvSupplier, InvGoods>(sql,
            (goods, product, supplier) => { goods.Product = product; goods.Supplier = supplier; return goods; },
            new { ProductId = productId, SupplierId = supplierId }, _uow.Transaction,
            splitOn: "PRODUCTNAME,SUPPLIERNAME");
        return result.FirstOrDefault();
    }

    public async Task AddAsync(InvGoods goods)
    {
        var sql = """
            INSERT INTO Inv_Goods (ProductID, SupplierID, SalePrice, Status, StorageReq, ShelfLifeHours, Description, CreateTime, UpdateTime)
            VALUES (:ProductID, :SupplierID, :SalePrice, :Status, :StorageReq, :ShelfLifeHours, :Description, :CreateTime, :UpdateTime)
            """;
        // 显式参数字典，避免把导航属性（Product/Supplier）交给 Dapper 导致绑定失败
        await _uow.Connection.ExecuteAsync(sql, ToParams(goods), _uow.Transaction);
    }

    public async Task UpdateAsync(InvGoods goods)
    {
        var sql = """
            UPDATE Inv_Goods
            SET SalePrice = :SalePrice, Status = :Status, StorageReq = :StorageReq,
                ShelfLifeHours = :ShelfLifeHours, Description = :Description, UpdateTime = :UpdateTime
            WHERE ProductID = :ProductID AND SupplierID = :SupplierID
            """;
        await _uow.Connection.ExecuteAsync(sql, ToParams(goods), _uow.Transaction);
    }

    private static Dictionary<string, object?> ToParams(InvGoods g) => new()
    {
        ["ProductID"] = g.ProductID,
        ["SupplierID"] = g.SupplierID,
        ["SalePrice"] = g.SalePrice,
        ["Status"] = g.Status,
        ["StorageReq"] = g.StorageReq,
        ["ShelfLifeHours"] = g.ShelfLifeHours,
        ["Description"] = g.Description,
        ["CreateTime"] = g.CreateTime,
        ["UpdateTime"] = g.UpdateTime
    };

    public async Task DeleteAsync(string productId, string supplierId)
    {
        var sql = "DELETE FROM Inv_Goods WHERE ProductID = :ProductId AND SupplierID = :SupplierId";
        await _uow.Connection.ExecuteAsync(sql, new { ProductId = productId, SupplierId = supplierId }, _uow.Transaction);
    }
}
