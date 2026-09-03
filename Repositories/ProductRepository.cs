using Dapper;
using FreshColdChain.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

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

    public async Task<List<InvProductImage>> GetAllProductImagesAsync()
    {
        var sql = """
            SELECT ImageID, ProductID, SupplierID, ImageUrl, SortOrder, CreateTime,
                   CASE WHEN DBMS_LOB.GETLENGTH(ImageData) IS NULL THEN 0 ELSE 1 END AS HasData
            FROM Inv_ProductImages
            ORDER BY ProductID, SortOrder
            """;
        var items = await _uow.Connection.QueryAsync<InvProductImage>(sql, null, _uow.Transaction);
        return items.ToList();
    }

    public async Task<List<InvProductImage>> GetProductImagesAsync(string productId)
    {
        var sql = """
            SELECT ImageID, ProductID, SupplierID, ImageUrl, SortOrder, CreateTime,
                   CASE WHEN DBMS_LOB.GETLENGTH(ImageData) IS NULL THEN 0 ELSE 1 END AS HasData
            FROM Inv_ProductImages
            WHERE ProductID = :ProductId
            ORDER BY SortOrder, CreateTime
            """;
        var items = await _uow.Connection.QueryAsync<InvProductImage>(sql, new { ProductId = productId }, _uow.Transaction);
        return items.ToList();
    }

    public async Task<InvProductImage?> GetProductImageByIdAsync(string imageId)
    {
        var sql = """
            SELECT ImageID, ProductID, SupplierID, ImageUrl, SortOrder, CreateTime
            FROM Inv_ProductImages
            WHERE ImageID = :ImageId
            """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<InvProductImage>(sql, new { ImageId = imageId }, _uow.Transaction);
    }

    public async Task AddProductImageAsync(InvProductImage image)
    {
        // BLOB 列不能用 Dapper 匿名参数插入，改用 ODP.NET 原生参数
        var cmd = (OracleCommand)_uow.Connection.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = """
            INSERT INTO Inv_ProductImages (ImageID, ProductID, ImageUrl, ImageData, ImageType, SortOrder, CreateTime)
            VALUES (:ImageID, :ProductID, :ImageUrl, :ImageData, :ImageType, :SortOrder, :CreateTime)
            """;
        cmd.Parameters.Add(new OracleParameter("ImageID", image.ImageID));
        cmd.Parameters.Add(new OracleParameter("ProductID", image.ProductID));
        cmd.Parameters.Add(new OracleParameter("ImageUrl", image.ImageUrl));
        cmd.Parameters.Add(new OracleParameter("ImageData", OracleDbType.Blob)
        {
            Value = image.ImageData ?? (object)DBNull.Value
        });
        cmd.Parameters.Add(new OracleParameter("ImageType", OracleDbType.Varchar2)
        {
            Value = image.ImageType ?? (object)DBNull.Value
        });
        cmd.Parameters.Add(new OracleParameter("SortOrder", image.SortOrder));
        cmd.Parameters.Add(new OracleParameter("CreateTime", image.CreateTime));
        if (_uow.Transaction is OracleTransaction oracleTx)
            cmd.Transaction = oracleTx;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(byte[]? Data, string? ContentType)?> GetProductImageDataAsync(string imageId)
    {
        // BLOB 读取：ODP.NET 返回 OracleBlob，取 .Value 得到 byte[]
        var cmd = (OracleCommand)_uow.Connection.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = "SELECT ImageData, ImageType FROM Inv_ProductImages WHERE ImageID = :ImageId";
        cmd.Parameters.Add(new OracleParameter("ImageId", imageId));

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        // ODP.NET 托管驱动索引器直接返回 byte[]；若驱动版本返回 OracleBlob 则取 .Value 兜底
        byte[]? data = reader["ImageData"] switch
        {
            byte[] bytes => bytes,
            OracleBlob blob => blob.Value,
            _ => null
        };
        var contentType = reader["ImageType"] as string;
        return (data, contentType);
    }

    public async Task DeleteProductImageAsync(string imageId)
    {
        var sql = "DELETE FROM Inv_ProductImages WHERE ImageID = :ImageId";
        await _uow.Connection.ExecuteAsync(sql, new { ImageId = imageId }, _uow.Transaction);
    }
}
