using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>A 组承运商查询，从 Oracle 基础运单读取，不暴露收货人信息。</summary>
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
            SELECT d.DeliveryID AS DeliveryId, d.OrderID AS OrderId, d.SupplierID AS SupplierId,
                COALESCE(x.TrackingNo, d.TrackingNo) AS TrackingNo, d.LogisticsStatus AS StatusCode, d.ShippedAt,
                CASE WHEN d.DeliveryID LIKE 'CARRIER-DEMO-%' OR d.OrderID LIKE 'ORDER-DEMO-%' THEN 1 ELSE 0 END AS IsDemoData
            FROM Log_ExpressDeliveries d LEFT JOIN Log_LogisticsDetails x ON x.DeliveryId = d.DeliveryID
            WHERE (:IncludeAll = 1 OR d.SupplierID IN :SupplierIds)
                AND (:Keyword IS NULL OR INSTR(d.OrderID, :Keyword) > 0
                     OR INSTR(COALESCE(x.TrackingNo, d.TrackingNo), :Keyword) > 0)
            ORDER BY d.ShippedAt DESC, d.DeliveryID FETCH FIRST 100 ROWS ONLY
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
