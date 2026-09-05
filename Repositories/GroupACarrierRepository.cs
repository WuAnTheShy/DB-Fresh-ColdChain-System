using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>A 组承运商查询，按真实订单的供应商包裹左连基础运单，不暴露收货人信息。</summary>
public interface IGroupACarrierRepository
{
    Task<IReadOnlyList<CarrierShipmentSummary>> SearchAsync(bool includeAll, string[] supplierIds, string? keyword, CancellationToken token);
    Task<LogExpressDelivery?> FindAsync(string deliveryId, bool includeAll, string[] supplierIds, IDbTransaction? transaction, CancellationToken token);
}

public sealed class GroupACarrierRepository(IUnitOfWork unitOfWork) : IGroupACarrierRepository
{
    public async Task<IReadOnlyList<CarrierShipmentSummary>> SearchAsync(bool includeAll, string[] supplierIds, string? keyword, CancellationToken token)
    {
        if (!includeAll && supplierIds.Length == 0) return [];
        return (await unitOfWork.Connection.QueryAsync<CarrierShipmentSummary>(new CommandDefinition("""
            WITH OrderPackages AS (
                SELECT o.OrderID, o.OrderNo, o.OrderStatus, o.CreatedAt, od.SupplierID
                FROM Biz_Orders o
                JOIN (SELECT DISTINCT OrderID, SupplierID FROM Biz_OrderDetails WHERE SupplierID IS NOT NULL) od
                    ON od.OrderID = o.OrderID
            )
            SELECT d.DeliveryID AS DeliveryId, p.OrderID AS OrderId, p.OrderNo, p.SupplierID AS SupplierId,
                COALESCE(x.TrackingNo, d.TrackingNo) AS TrackingNo, d.LogisticsStatus AS StatusCode,
                p.OrderStatus AS OrderStatusCode, p.CreatedAt, d.ShippedAt
            FROM OrderPackages p
            LEFT JOIN Log_ExpressDeliveries d ON d.OrderID = p.OrderID AND d.SupplierID = p.SupplierID
            LEFT JOIN Log_LogisticsDetails x ON x.DeliveryId = d.DeliveryID
            WHERE (:IncludeAll = 1 OR p.SupplierID IN :SupplierIds)
                AND (d.DeliveryID IS NOT NULL OR p.OrderStatus IN ('PAID', 'SHIPPED', 'COMPLETED', 'DELIVERED'))
                AND (:Keyword IS NULL OR INSTR(p.OrderID, :Keyword) > 0
                     OR INSTR(p.OrderNo, :Keyword) > 0
                     OR INSTR(COALESCE(x.TrackingNo, d.TrackingNo), :Keyword) > 0)
            ORDER BY CASE WHEN d.DeliveryID IS NULL THEN 1 ELSE 0 END,
                COALESCE(d.ShippedAt, p.CreatedAt) DESC, p.OrderID, p.SupplierID
            FETCH FIRST 100 ROWS ONLY
            """, new { IncludeAll = includeAll ? 1 : 0, SupplierIds = supplierIds.Length == 0 ? ["__NO_SCOPE__"] : supplierIds,
                Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim() },
            unitOfWork.Transaction, cancellationToken: token))).ToList();
    }

    public Task<LogExpressDelivery?> FindAsync(string deliveryId, bool includeAll, string[] supplierIds, IDbTransaction? transaction, CancellationToken token)
    {
        if (!includeAll && supplierIds.Length == 0) return Task.FromResult<LogExpressDelivery?>(null);
        var sql = "SELECT * FROM Log_ExpressDeliveries WHERE DeliveryID=:DeliveryId AND (:IncludeAll=1 OR SupplierID IN :SupplierIds)";
        if (transaction != null) sql += " FOR UPDATE";
        return (transaction?.Connection ?? unitOfWork.Connection).QuerySingleOrDefaultAsync<LogExpressDelivery>(
            new CommandDefinition(sql, new { DeliveryId = deliveryId, IncludeAll = includeAll ? 1 : 0,
                SupplierIds = supplierIds.Length == 0 ? ["__NO_SCOPE__"] : supplierIds }, transaction, cancellationToken: token));
    }
}
