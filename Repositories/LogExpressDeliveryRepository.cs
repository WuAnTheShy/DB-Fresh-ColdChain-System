using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 冷链发货单仓储实现
/// </summary>
public class LogExpressDeliveryRepository : BaseRepository<LogExpressDelivery>, ILogExpressDeliveryRepository
{
    public LogExpressDeliveryRepository(IUnitOfWork uow) : base(uow) { }

    /// <summary>按订单 ID 查询发货单列表</summary>
    public async Task<List<LogExpressDelivery>> GetByOrderIdAsync(string orderId)
    {
        var sql = """SELECT * FROM "Log_ExpressDeliveries" WHERE "OrderID" = :Id ORDER BY "ShippedAt" DESC """;
        return (await _uow.Connection.QueryAsync<LogExpressDelivery>(sql, new { Id = orderId }, _uow.Transaction)).ToList();
    }

    /// <summary>按供应商 ID 查询发货单列表</summary>
    public async Task<List<LogExpressDelivery>> GetBySupplierIdAsync(string supplierId)
    {
        var sql = """SELECT * FROM "Log_ExpressDeliveries" WHERE "SupplierID" = :Id ORDER BY "ShippedAt" DESC """;
        return (await _uow.Connection.QueryAsync<LogExpressDelivery>(sql, new { Id = supplierId }, _uow.Transaction)).ToList();
    }

    /// <summary>按物流单号精确查询</summary>
    public async Task<LogExpressDelivery?> GetByTrackingNoAsync(string trackingNo)
    {
        var sql = """SELECT * FROM "Log_ExpressDeliveries" WHERE "TrackingNo" = :No """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<LogExpressDelivery>(sql, new { No = trackingNo }, _uow.Transaction);
    }
}
