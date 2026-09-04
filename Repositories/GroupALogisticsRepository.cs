using System.Data;
using Dapper;
using FreshColdChain.Models;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Repositories;

/// <summary>A 组物流表仓储，不新建写连接、不提交或释放调用方事务。</summary>
public sealed class GroupALogisticsRepository(IUnitOfWork unitOfWork) : IGroupALogisticsRepository
{
    public Task<LogExpressDelivery?> GetDeliveryAsync(string orderId, string supplierId, bool forUpdate,
        IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        transaction ??= unitOfWork.Transaction;
        if (forUpdate) _ = WriteConnection(transaction);
        var sql = "SELECT * FROM Log_ExpressDeliveries WHERE OrderID = :OrderId AND SupplierID = :SupplierId";
        if (forUpdate) sql += " FOR UPDATE";
        return ReadConnection(transaction).QuerySingleOrDefaultAsync<LogExpressDelivery>(new CommandDefinition(
            sql, new { OrderId = orderId, SupplierId = supplierId }, transaction, cancellationToken: cancellationToken));
    }

    public Task<LogLogisticsDetail?> GetDetailAsync(string deliveryId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        transaction ??= unitOfWork.Transaction;
        return ReadConnection(transaction).QuerySingleOrDefaultAsync<LogLogisticsDetail>(new CommandDefinition(
            "SELECT * FROM Log_LogisticsDetails WHERE DeliveryId = :DeliveryId", new { DeliveryId = deliveryId },
            transaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LogLogisticsEvent>> GetEventsAsync(string deliveryId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        transaction ??= unitOfWork.Transaction;
        return (await ReadConnection(transaction).QueryAsync<LogLogisticsEvent>(new CommandDefinition(
            "SELECT * FROM Log_LogisticsEvents WHERE DeliveryId = :DeliveryId ORDER BY SequenceNo",
            new { DeliveryId = deliveryId }, transaction, cancellationToken: cancellationToken))).ToList();
    }

    public Task<LogLogisticsEvent?> GetEventAsync(string eventId, IDbTransaction transaction,
        CancellationToken cancellationToken = default) => WriteConnection(transaction)
        .QuerySingleOrDefaultAsync<LogLogisticsEvent>(new CommandDefinition(
            "SELECT * FROM Log_LogisticsEvents WHERE EventId = :EventId", new { EventId = eventId },
            transaction, cancellationToken: cancellationToken));

    public async Task InsertDetailAsync(LogLogisticsDetail detail, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteConnection(transaction).ExecuteAsync(new CommandDefinition("""
                INSERT INTO Log_LogisticsDetails
                    (DeliveryId, CarrierCode, CarrierName, TrackingNo, PackageTemperature, EstimatedArrivalAt, CarrierTrackingKey, Remark)
                VALUES (:DeliveryId, :CarrierCode, :CarrierName, :TrackingNo, :PackageTemperature, :EstimatedArrivalAt, :CarrierTrackingKey, :Remark)
                """, new { detail.DeliveryId, detail.CarrierCode, detail.CarrierName, detail.TrackingNo,
                    detail.PackageTemperature, EstimatedArrivalAt = new OracleTimestamp(detail.EstimatedArrivalAt),
                    detail.CarrierTrackingKey, detail.Remark }, transaction, cancellationToken: cancellationToken));
        }
        catch (OracleException exception) when (exception.Number == 1)
        {
            throw new LogisticsWriteConflictException("承运商运单号已被其他发货单使用，或发货扩展记录已存在", exception);
        }
    }

    public async Task InsertEventAsync(LogLogisticsEvent item, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteConnection(transaction).ExecuteAsync(new CommandDefinition("""
                INSERT INTO Log_LogisticsEvents
                    (EventId, DeliveryId, RequestHash, SequenceNo, StatusCode, Location, Description,
                     OccurredAt, TemperatureCelsius, IsTemperatureException)
                VALUES (:EventId, :DeliveryId, :RequestHash, :SequenceNo, :StatusCode, :Location, :Description,
                        :OccurredAt, :TemperatureCelsius, :IsTemperatureException)
                """, new { item.EventId, item.DeliveryId, item.RequestHash, item.SequenceNo, item.StatusCode,
                    item.Location, item.Description, OccurredAt = new OracleTimestamp(item.OccurredAt),
                    item.TemperatureCelsius, item.IsTemperatureException }, transaction, cancellationToken: cancellationToken));
        }
        catch (OracleException exception) when (exception.Number == 1)
        {
            throw new LogisticsWriteConflictException("物流事件编号或事件序号冲突，请刷新后重试", exception);
        }
    }

    public async Task UpdateBaseStatusAsync(string deliveryId, string status, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var affected = await WriteConnection(transaction).ExecuteAsync(new CommandDefinition(
            "UPDATE Log_ExpressDeliveries SET LogisticsStatus = :Status WHERE DeliveryID = :DeliveryId",
            new { DeliveryId = deliveryId, Status = status }, transaction, cancellationToken: cancellationToken));
        if (affected != 1) throw new InvalidOperationException("物流基础发货单状态更新失败");
    }

    private IDbConnection ReadConnection(IDbTransaction? transaction) =>
        transaction == null ? unitOfWork.Connection : WriteConnection(transaction);

    // 显式绑定 TIMESTAMP，避免驱动按 Oracle DATE 绑定而丢失事件的亚秒精度。
    private sealed class OracleTimestamp(DateTime? value) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name) => command.Parameters.Add(
            new OracleParameter(name, OracleDbType.TimeStamp) { Value = value.HasValue ? value.Value : DBNull.Value });
    }

    private static IDbConnection WriteConnection(IDbTransaction? transaction) =>
        transaction?.Connection is { State: ConnectionState.Open } connection ? connection
            : throw new InvalidOperationException("物流写入必须使用有效的调用方事务");
}
