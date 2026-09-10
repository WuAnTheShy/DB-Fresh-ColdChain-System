using Dapper;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Repositories;

// 冷链发货单仓储实现
public class LogExpressDeliveryRepository : BaseRepository<LogExpressDelivery>, ILogExpressDeliveryRepository
{
    public LogExpressDeliveryRepository(IUnitOfWork uow) : base(uow) { }

    // 按订单 ID 查询发货单列表
    public async Task<List<LogExpressDelivery>> GetByOrderIdAsync(string orderId)
    {
        var sql = """SELECT * FROM Log_ExpressDeliveries WHERE OrderID = :Id ORDER BY ShippedAt DESC """;
        return (await _uow.Connection.QueryAsync<LogExpressDelivery>(sql, new { Id = orderId }, _uow.Transaction)).ToList();
    }

    // 按供应商 ID 查询发货单列表
    public async Task<List<LogExpressDelivery>> GetBySupplierIdAsync(string supplierId)
    {
        var sql = """SELECT * FROM Log_ExpressDeliveries WHERE SupplierID = :Id ORDER BY ShippedAt DESC """;
        return (await _uow.Connection.QueryAsync<LogExpressDelivery>(sql, new { Id = supplierId }, _uow.Transaction)).ToList();
    }

    // 按物流单号精确查询
    public async Task<LogExpressDelivery?> GetByTrackingNoAsync(string trackingNo)
    {
        var sql = """SELECT * FROM Log_ExpressDeliveries WHERE TrackingNo = :No """;
        return await _uow.Connection.QuerySingleOrDefaultAsync<LogExpressDelivery>(sql, new { No = trackingNo }, _uow.Transaction);
    }

    // 只列出已有发货单的订单；LEFT JOIN B 组 Biz_Orders 取展示用订单号
    public async Task<List<ShippedOrderOptionDto>> GetDistinctOrdersAsync()
    {
        var sql = """
            SELECT DISTINCT d.OrderID, o.OrderNo
            FROM Log_ExpressDeliveries d
            LEFT JOIN Biz_Orders o ON d.OrderID = o.OrderId
            ORDER BY o.OrderNo NULLS LAST, d.OrderID
            """;
        return (await _uow.Connection.QueryAsync<ShippedOrderOptionDto>(sql, transaction: _uow.Transaction)).ToList();
    }
}
