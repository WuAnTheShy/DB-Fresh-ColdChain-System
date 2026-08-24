using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class ProductRepository : BaseRepository<InvProduct>, IProductRepository
{
    public ProductRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var where = string.IsNullOrWhiteSpace(keyword) ? ""
            : """ WHERE p.ProductName LIKE '%' || :Keyword || '%' """;

        var countSql = $"""SELECT COUNT(*) FROM Inv_Products p {where}""";
        var dataSql = $"""
            SELECT p.*, c.*, s.*, st.*
            FROM Inv_Products p
            LEFT JOIN Inv_Category c ON p.CategoryID = c.CategoryID
            LEFT JOIN Inv_Suppliers s ON p.SupplierID = s.SupplierID
            LEFT JOIN Inv_StockSummary st ON p.ProductID = st.ProductID
            {where}
            ORDER BY p.ProductID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        var dp = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(keyword)) dp.Add("Keyword", keyword);
        dp.Add("Skip", (pageIndex - 1) * pageSize);
        dp.Add("Take", pageSize);

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, dp, _uow.Transaction);
        var items = await _uow.Connection.QueryAsync<InvProduct, InvCategory, InvSupplier, InvStockSummary, InvProduct>(
            dataSql,
            (prod, cat, sup, st) => { prod.Category = cat; prod.Supplier = sup; prod.StockSummary = st; return prod; },
            dp, _uow.Transaction, splitOn: "CATEGORYNAME,SUPPLIERNAME,STOCKID");

        return (items.ToList(), total);
    }

    public async Task<InvProduct?> GetByIdWithDetailsAsync(string id)
    {
        var sql = """
            SELECT p.*, c.*, s.*, st.*
            FROM Inv_Products p
            LEFT JOIN Inv_Category c ON p.CategoryID = c.CategoryID
            LEFT JOIN Inv_Suppliers s ON p.SupplierID = s.SupplierID
            LEFT JOIN Inv_StockSummary st ON p.ProductID = st.ProductID
            WHERE p.ProductID = :Id
            """;
        var result = await _uow.Connection.QueryAsync<InvProduct, InvCategory, InvSupplier, InvStockSummary, InvProduct>(
            sql,
            (prod, cat, sup, st) => { prod.Category = cat; prod.Supplier = sup; prod.StockSummary = st; return prod; },
            new { Id = id }, _uow.Transaction, splitOn: "CATEGORYNAME,SUPPLIERNAME,STOCKID");
        return result.FirstOrDefault();
    }

    /// <summary>
    /// 按供应商集合分页查询上架商品（含库存汇总）。
    /// 可售 = 上架（Status='ACTIVE'）；库存是否充足由 AvailableQty 与前端 StockStatus 表达。
    /// </summary>
    public async Task<(List<InvProduct> Items, int Total)> GetPagedBySuppliersAsync(
        IReadOnlyList<string> supplierIds, string? keyword, string? categoryId, int pageIndex, int pageSize)
    {
        if (supplierIds.Count == 0)
            return (new List<InvProduct>(), 0);

        var conditions = new List<string>();
        var dp = new Dictionary<string, object?>();

        // 供应商集合 IN 条件
        var inNames = new List<string>(supplierIds.Count);
        for (var i = 0; i < supplierIds.Count; i++)
        {
            var p = $"S{i}";
            inNames.Add($":{p}");
            dp[p] = supplierIds[i];
        }
        conditions.Add($"p.SupplierID IN ({string.Join(", ", inNames)})");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("p.ProductName LIKE '%' || :Keyword || '%'");
            dp["Keyword"] = keyword;
        }
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            conditions.Add("p.CategoryID = :CategoryId");
            dp["CategoryId"] = categoryId;
        }
        conditions.Add("p.Status = 'ACTIVE'");

        var where = " WHERE " + string.Join(" AND ", conditions);

        var countSql = $"SELECT COUNT(*) FROM Inv_Products p {where}";
        var dataSql = $"""
            SELECT p.*, st.*
            FROM Inv_Products p
            LEFT JOIN Inv_StockSummary st ON p.ProductID = st.ProductID
            {where}
            ORDER BY p.ProductID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;

        dp["Skip"] = (pageIndex - 1) * pageSize;
        dp["Take"] = pageSize;

        var total = await _uow.Connection.ExecuteScalarAsync<int>(countSql, dp, _uow.Transaction);
        var items = await _uow.Connection.QueryAsync<InvProduct, InvStockSummary, InvProduct>(
            dataSql,
            (prod, st) => { prod.StockSummary = st; return prod; },
            dp, _uow.Transaction, splitOn: "STOCKID");

        return (items.ToList(), total);
    }

    /// <summary>按商品编号集合批量查询（含库存汇总）</summary>
    public async Task<List<InvProduct>> GetByIdsWithDetailsAsync(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
            return new List<InvProduct>();

        var inNames = new List<string>(ids.Count);
        var dp = new Dictionary<string, object?>();
        for (var i = 0; i < ids.Count; i++)
        {
            var p = $"Id{i}";
            inNames.Add($":{p}");
            dp[p] = ids[i];
        }

        var sql = $"""
            SELECT p.*, st.*
            FROM Inv_Products p
            LEFT JOIN Inv_StockSummary st ON p.ProductID = st.ProductID
            WHERE p.ProductID IN ({string.Join(", ", inNames)})
            """;

        var items = await _uow.Connection.QueryAsync<InvProduct, InvStockSummary, InvProduct>(
            sql,
            (prod, st) => { prod.StockSummary = st; return prod; },
            dp, _uow.Transaction, splitOn: "STOCKID");

        return items.ToList();
    }
}
