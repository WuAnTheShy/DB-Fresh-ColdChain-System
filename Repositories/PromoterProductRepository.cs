using Dapper;
using FreshColdChain.Models.DTOs;
using System.Data;

namespace FreshColdChain.Repositories;

/// <summary>
/// 商品入团表（CRM_PRODUCT_ENTRIES）仓库实现。
/// </summary>
public class PromoterProductRepository : IPromoterProductRepository
{
    private readonly IUnitOfWork _uow;

    public PromoterProductRepository(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> AddOrUpdateEntryAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice = null, string? promoterDesc = null, string status = "Active", IDbTransaction? transaction = null)
    {
        const string sql = @"
            MERGE INTO CRM_PRODUCT_ENTRIES T
            USING (SELECT :PromoterId AS PROMOTERID, :ProductId AS PRODUCTID, :SupplierId AS SUPPLIERID FROM DUAL) S
            ON (T.PROMOTERID = S.PROMOTERID AND T.PRODUCTID = S.PRODUCTID AND T.SUPPLIERID = S.SUPPLIERID)
            WHEN MATCHED THEN
                UPDATE SET STATUS = :Status, PROMOTERPRICE = :PromoterPrice,
                           PROMOTERDESC = NVL(:PromoterDesc, T.PROMOTERDESC), UPDATETIME = SYSDATE
            WHEN NOT MATCHED THEN
                INSERT (PROMOTERID, PRODUCTID, SUPPLIERID, STATUS, PROMOTERPRICE, PROMOTERDESC, CREATETIME)
                VALUES (S.PROMOTERID, S.PRODUCTID, S.SUPPLIERID, :Status, :PromoterPrice, :PromoterDesc, SYSDATE)";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId, Status = status, PromoterPrice = promoterPrice, PromoterDesc = promoterDesc },
            transaction);
        return rows > 0;
    }

    public async Task<bool> UpdateEntryDescriptionAsync(string promoterId, string productId, string supplierId, string? promoterDesc, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PRODUCT_ENTRIES
            SET PROMOTERDESC = :PromoterDesc, UPDATETIME = SYSDATE
            WHERE PROMOTERID = :PromoterId AND PRODUCTID = :ProductId AND SUPPLIERID = :SupplierId AND STATUS = 'Active'";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId, PromoterDesc = promoterDesc },
            transaction);
        return rows > 0;
    }

    public async Task<bool> SoftDeleteEntryAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PRODUCT_ENTRIES
            SET STATUS = 'Inactive', UPDATETIME = SYSDATE
            WHERE PROMOTERID = :PromoterId AND PRODUCTID = :ProductId AND SUPPLIERID = :SupplierId";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId },
            transaction);
        return rows > 0;
    }

    public async Task<bool> UpdateEntryPriceAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PRODUCT_ENTRIES
            SET PROMOTERPRICE = :PromoterPrice, UPDATETIME = SYSDATE
            WHERE PROMOTERID = :PromoterId AND PRODUCTID = :ProductId AND SUPPLIERID = :SupplierId AND STATUS = 'Active'";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId, PromoterPrice = promoterPrice },
            transaction);
        return rows > 0;
    }

    public async Task<List<PromoterProductEntryDetailDto>> GetActiveEntriesDetailAsync(string promoterId, IDbTransaction? transaction = null)
    {
        // 表/列名以共享库（COLDCHAIN）实际结构为准：INV_SUPPLIERPRICES、DEFAULTPRICE、SUPPLYPRICE
        const string sql = @"
            SELECT E.PRODUCTID, E.SUPPLIERID, E.PROMOTERPRICE, E.PROMOTERDESC,
                   P.PRODUCTNAME, P.UNIT, P.DEFAULTPRICE, P.DESCRIPTION,
                   S.SUPPLIERNAME,
                   SP.SUPPLYPRICE
            FROM CRM_PRODUCT_ENTRIES E
            JOIN INV_PRODUCTS P ON P.PRODUCTID = E.PRODUCTID
            JOIN INV_SUPPLIERS S ON S.SUPPLIERID = E.SUPPLIERID
            LEFT JOIN INV_SUPPLIERPRICES SP ON SP.SUPPLIERID = E.SUPPLIERID AND SP.PRODUCTID = E.PRODUCTID
            WHERE E.PROMOTERID = :PromoterId AND E.STATUS = 'Active'
            ORDER BY E.CREATETIME DESC, E.PRODUCTID";
        var result = await _uow.Connection.QueryAsync<PromoterProductEntryDetailDto>(sql,
            new { PromoterId = promoterId }, transaction);
        return result.ToList();
    }

    public async Task<List<(string ProductId, string SupplierId, decimal? PromoterPrice)>> GetActiveEntriesByPromoterAsync(string promoterId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT PRODUCTID, SUPPLIERID, PROMOTERPRICE
            FROM CRM_PRODUCT_ENTRIES
            WHERE PROMOTERID = :PromoterId AND STATUS = 'Active'";
        var result = await _uow.Connection.QueryAsync<(string ProductId, string SupplierId, decimal? PromoterPrice)>(sql,
            new { PromoterId = promoterId }, transaction);
        return result.ToList();
    }

    public async Task<bool> IsEntryActiveAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM CRM_PRODUCT_ENTRIES
            WHERE PROMOTERID = :PromoterId AND PRODUCTID = :ProductId AND SUPPLIERID = :SupplierId AND STATUS = 'Active'";
        var count = await _uow.Connection.ExecuteScalarAsync<int>(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId },
            transaction);
        return count > 0;
    }
}
