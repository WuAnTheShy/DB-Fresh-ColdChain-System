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

    public async Task<bool> AddOrUpdateEntryAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice = null, decimal? supplyPrice = null, decimal? defaultPrice = null, string? promoterDesc = null, string status = "Active", IDbTransaction? transaction = null)
    {
        // supplyPrice/defaultPrice 为入团时刻“供应商动态定价”快照（报价 / 推荐价=报价×1.2），
        // 重复入团（恢复 Active）时同步刷新快照为最新动态价。
        const string sql = @"
            MERGE INTO CRM_PRODUCT_ENTRIES T
            USING (SELECT :PromoterId AS PROMOTERID, :ProductId AS PRODUCTID, :SupplierId AS SUPPLIERID FROM DUAL) S
            ON (T.PROMOTERID = S.PROMOTERID AND T.PRODUCTID = S.PRODUCTID AND T.SUPPLIERID = S.SUPPLIERID)
            WHEN MATCHED THEN
                UPDATE SET STATUS = :Status, PROMOTERPRICE = :PromoterPrice,
                           SUPPLYPRICE = NVL(:SupplyPrice, T.SUPPLYPRICE),
                           DEFAULTPRICE = NVL(:DefaultPrice, T.DEFAULTPRICE),
                           PROMOTERDESC = NVL(:PromoterDesc, T.PROMOTERDESC), UPDATETIME = SYSDATE
            WHEN NOT MATCHED THEN
                INSERT (PROMOTERID, PRODUCTID, SUPPLIERID, STATUS, PROMOTERPRICE,
                        SUPPLYPRICE, DEFAULTPRICE, PROMOTERDESC, CREATETIME)
                VALUES (S.PROMOTERID, S.PRODUCTID, S.SUPPLIERID, :Status, :PromoterPrice,
                        :SupplyPrice, :DefaultPrice, :PromoterDesc, SYSDATE)";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId, Status = status, PromoterPrice = promoterPrice, SupplyPrice = supplyPrice, DefaultPrice = defaultPrice, PromoterDesc = promoterDesc },
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
        // 报价/推荐价以入团时刻“供应商动态定价”快照（CRM_PRODUCT_ENTRIES.SUPPLYPRICE/DEFAULTPRICE）为准：
        // 报价=规则引擎计算值（无规则时=货物售价），推荐价=报价×1.2（倍率不变）；
        // 快照为空（历史兜底）时回退为货物售价实时派生。
        const string sql = @"
            SELECT E.PRODUCTID, E.SUPPLIERID, E.PROMOTERPRICE, E.PROMOTERDESC,
                   E.CREATETIME AS CreateTime,
                   P.PRODUCTNAME, P.UNIT,
                   S.SUPPLIERNAME,
                   COALESCE(E.SUPPLYPRICE, G.SALEPRICE, 0) AS SUPPLYPRICE,
                   COALESCE(E.DEFAULTPRICE, ROUND(COALESCE(E.SUPPLYPRICE, G.SALEPRICE, 0) * 1.2, 2), 0) AS DEFAULTprice,
                   COALESCE(G.DESCRIPTION, P.DESCRIPTION) AS DESCRIPTION,
                   COALESCE(G.STATUS, P.STATUS) AS ProductStatus
            FROM CRM_PRODUCT_ENTRIES E
            JOIN INV_PRODUCTS P ON P.PRODUCTID = E.PRODUCTID
            JOIN INV_SUPPLIERS S ON S.SUPPLIERID = E.SUPPLIERID
            LEFT JOIN INV_GOODS G ON G.SUPPLIERID = E.SUPPLIERID AND G.PRODUCTID = E.PRODUCTID
            WHERE E.PROMOTERID = :PromoterId AND E.STATUS = 'Active'
            ORDER BY E.CREATETIME DESC, E.PRODUCTID";
        var result = await _uow.Connection.QueryAsync<PromoterProductEntryDetailDto>(sql,
            new { PromoterId = promoterId }, transaction);
        return result.ToList();
    }

    public async Task<PromoterProductEntryDetailDto?> GetActiveEntryDetailAsync(string promoterId, string productId, string supplierId, IDbTransaction? transaction = null)
    {
        // 报价/推荐价读取入团快照（CRM_PRODUCT_ENTRIES.SUPPLYPRICE/DEFAULTPRICE），为空时回退货物售价派生
        const string sql = @"
            SELECT E.PRODUCTID, E.SUPPLIERID, E.PROMOTERPRICE, E.PROMOTERDESC,
                   P.PRODUCTNAME, P.UNIT,
                   S.SUPPLIERNAME,
                   COALESCE(E.SUPPLYPRICE, G.SALEPRICE, 0) AS SUPPLYPRICE,
                   COALESCE(E.DEFAULTPRICE, ROUND(COALESCE(E.SUPPLYPRICE, G.SALEPRICE, 0) * 1.2, 2), 0) AS DEFAULTprice,
                   COALESCE(G.DESCRIPTION, P.DESCRIPTION) AS DESCRIPTION,
                   COALESCE(G.STATUS, P.STATUS) AS ProductStatus
            FROM CRM_PRODUCT_ENTRIES E
            JOIN INV_PRODUCTS P ON P.PRODUCTID = E.PRODUCTID
            JOIN INV_SUPPLIERS S ON S.SUPPLIERID = E.SUPPLIERID
            LEFT JOIN INV_GOODS G ON G.SUPPLIERID = E.SUPPLIERID AND G.PRODUCTID = E.PRODUCTID
            WHERE E.PROMOTERID = :PromoterId AND E.PRODUCTID = :ProductId AND E.SUPPLIERID = :SupplierId AND E.STATUS = 'Active'";
        return await _uow.Connection.QueryFirstOrDefaultAsync<PromoterProductEntryDetailDto>(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId }, transaction);
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

    public async Task<List<(string PromoterId, decimal? PromoterPrice)>> GetActiveListedEntriesAsync(string supplierId, string productId, IDbTransaction? transaction = null)
    {
        // 供应商定价规则变化后，找出该供应商×商品全部团长已上架条目进行快照重算
        const string sql = @"
            SELECT PROMOTERID AS PromoterId, PROMOTERPRICE AS PromoterPrice
            FROM CRM_PRODUCT_ENTRIES
            WHERE SUPPLIERID = :SupplierId AND PRODUCTID = :ProductId AND STATUS = 'Active'";
        var result = await _uow.Connection.QueryAsync<(string PromoterId, decimal? PromoterPrice)>(sql,
            new { SupplierId = supplierId, ProductId = productId }, transaction);
        return result.ToList();
    }

    public async Task<int> RefreshListedEntrySnapshotAsync(string promoterId, string productId, string supplierId, decimal supplyPrice, decimal defaultPrice, decimal promoterPrice, IDbTransaction? transaction = null)
    {
        // 以规则引擎最新结果覆盖入团快照；promoterPrice 为调用方按新范围钳制后的团长定价
        const string sql = @"
            UPDATE CRM_PRODUCT_ENTRIES
            SET SUPPLYPRICE = :SupplyPrice, DEFAULTPRICE = :DefaultPrice,
                PROMOTERPRICE = :PromoterPrice, UPDATETIME = SYSDATE
            WHERE PROMOTERID = :PromoterId AND PRODUCTID = :ProductId
              AND SUPPLIERID = :SupplierId AND STATUS = 'Active'";
        var rows = await _uow.Connection.ExecuteAsync(sql,
            new { PromoterId = promoterId, ProductId = productId, SupplierId = supplierId, SupplyPrice = supplyPrice, DefaultPrice = defaultPrice, PromoterPrice = promoterPrice },
            transaction);
        return rows;
    }
}
