using System.Collections.Concurrent;
using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

/// <summary>
/// A 组高级物流接口未就绪时的隔离兜底。
/// 数据只保存在当前进程内并显式标记为 FALLBACK，不写 A/B/C 任一业务表。
/// </summary>
public sealed class FallbackGroupALogisticsExtensionProvider(
    IOptions<GroupALogisticsFallbackOptions> options) : IGroupALogisticsExtensionProvider
{
    private readonly GroupALogisticsFallbackOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, SupplierLogisticsSnapshot> _snapshots =
        new(StringComparer.Ordinal);

    public Task<SupplierLogisticsSnapshot> RegisterShipmentAsync(
        LogisticsShipmentRegistration registration,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        var command = registration.Command;
        var shippedAt = registration.ShippedAt;
        var snapshot = new SupplierLogisticsSnapshot
        {
            OrderId = registration.OrderId,
            SupplierId = registration.SupplierId,
            DeliveryId = registration.DeliveryId,
            CarrierCode = ValueOrDefault(command.CarrierCode, _options.CarrierCode),
            CarrierName = ValueOrDefault(command.CarrierName, _options.CarrierName),
            TrackingNo = ValueOrDefault(command.TrackingNo, registration.BaseTrackingNo),
            PackageTemperature = command.PackageTemperature,
            StatusCode = LogisticsStatusCodes.Normalize(registration.BaseStatus),
            ShippedAt = shippedAt,
            EstimatedArrivalAt = command.EstimatedArrivalAt ??
                shippedAt.AddHours(_options.EstimatedTransitHours),
            DataSource = LogisticsDataSources.Fallback,
            Events =
            [
                CreateEvent(
                    LogisticsStatusCodes.Shipped,
                    _options.OriginLocation,
                    _options.ShippedDescription,
                    shippedAt,
                    null,
                    command.PackageTemperature)
            ]
        };

        _snapshots[CreateKey(registration.OrderId, registration.SupplierId)] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<SupplierLogisticsSnapshot> GetSnapshotAsync(
        LogisticsTraceSeed seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        cancellationToken.ThrowIfCancellationRequested();

        if (_snapshots.TryGetValue(CreateKey(seed.OrderId, seed.SupplierId), out var stored))
        {
            var evaluated = EvaluateDelay(MergeBaseTrace(stored, seed));
            _snapshots[CreateKey(seed.OrderId, seed.SupplierId)] = evaluated;
            return Task.FromResult(evaluated);
        }

        var status = LogisticsStatusCodes.Normalize(seed.StatusCode);
        var events = seed.ShippedAt.HasValue && LogisticsStatusCodes.IsShippedOrLater(status)
            ? new[]
            {
                CreateEvent(
                    LogisticsStatusCodes.Shipped,
                    _options.OriginLocation,
                    _options.ShippedDescription,
                    seed.ShippedAt.Value,
                    null,
                    "CHILLED")
            }
            : [];
        var snapshot = new SupplierLogisticsSnapshot
        {
            OrderId = seed.OrderId,
            SupplierId = seed.SupplierId,
            DeliveryId = seed.DeliveryId,
            CarrierCode = status == LogisticsStatusCodes.Pending ? null : _options.CarrierCode,
            CarrierName = status == LogisticsStatusCodes.Pending ? null : _options.CarrierName,
            TrackingNo = seed.TrackingNo,
            StatusCode = status,
            ShippedAt = seed.ShippedAt,
            EstimatedArrivalAt = seed.ShippedAt?.AddHours(_options.EstimatedTransitHours),
            DataSource = LogisticsDataSources.Fallback,
            Events = events
        };

        return Task.FromResult(EvaluateDelay(snapshot));
    }

    public Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
        LogisticsTrackingEventCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(command.OrderId, command.SupplierId);
        if (!_snapshots.TryGetValue(key, out var current))
            throw new OrderBusinessException("尚未登记该供应商的发货信息");
        if (current.Events.Any(item => string.Equals(
            item.EventId,
            command.EventId,
            StringComparison.Ordinal)))
            return Task.FromResult(current);

        var status = LogisticsStatusCodes.Normalize(command.StatusCode);
        var isTemperatureException = IsTemperatureException(
            current.PackageTemperature,
            command.TemperatureCelsius);
        var nextEvents = current.Events
            .Append(CreateEvent(
                status,
                command.Location,
                command.Description,
                command.OccurredAt,
                command.TemperatureCelsius,
                current.PackageTemperature,
                command.EventId))
            .OrderBy(item => item.OccurredAt)
            .ToList();
        var hasException = current.HasException ||
            status == LogisticsStatusCodes.Exception ||
            isTemperatureException;

        var updated = new SupplierLogisticsSnapshot
        {
            OrderId = current.OrderId,
            SupplierId = current.SupplierId,
            DeliveryId = current.DeliveryId,
            CarrierCode = current.CarrierCode,
            CarrierName = current.CarrierName,
            TrackingNo = current.TrackingNo,
            PackageTemperature = current.PackageTemperature,
            StatusCode = isTemperatureException ? LogisticsStatusCodes.Exception : status,
            ShippedAt = current.ShippedAt,
            EstimatedArrivalAt = current.EstimatedArrivalAt,
            DeliveredAt = status == LogisticsStatusCodes.Delivered
                ? command.OccurredAt
                : current.DeliveredAt,
            HasException = hasException,
            ExceptionMessage = isTemperatureException
                ? "运输温度超出商品温区范围"
                : status == LogisticsStatusCodes.Exception
                    ? command.Description
                    : current.ExceptionMessage,
            DataSource = LogisticsDataSources.Fallback,
            Events = nextEvents
        };

        _snapshots[key] = updated;
        return Task.FromResult(updated);
    }

    private SupplierLogisticsSnapshot MergeBaseTrace(
        SupplierLogisticsSnapshot stored,
        LogisticsTraceSeed seed)
    {
        var baseStatus = LogisticsStatusCodes.Normalize(seed.StatusCode);
        var status = baseStatus == LogisticsStatusCodes.Pending
            ? stored.StatusCode
            : baseStatus;
        return new SupplierLogisticsSnapshot
        {
            OrderId = stored.OrderId,
            SupplierId = stored.SupplierId,
            DeliveryId = seed.DeliveryId ?? stored.DeliveryId,
            CarrierCode = stored.CarrierCode,
            CarrierName = stored.CarrierName,
            TrackingNo = stored.TrackingNo ?? seed.TrackingNo,
            PackageTemperature = stored.PackageTemperature,
            StatusCode = status,
            ShippedAt = seed.ShippedAt ?? stored.ShippedAt,
            EstimatedArrivalAt = stored.EstimatedArrivalAt,
            DeliveredAt = stored.DeliveredAt,
            HasException = stored.HasException,
            ExceptionMessage = stored.ExceptionMessage,
            DataSource = LogisticsDataSources.Fallback,
            Events = stored.Events
        };
    }

    private SupplierLogisticsSnapshot EvaluateDelay(SupplierLogisticsSnapshot snapshot)
    {
        if (!snapshot.EstimatedArrivalAt.HasValue ||
            snapshot.EstimatedArrivalAt.Value >= DateTime.Now ||
            snapshot.HasException ||
            snapshot.StatusCode is LogisticsStatusCodes.Delivered or LogisticsStatusCodes.Returned)
            return snapshot;

        var detectedAt = DateTime.Now;
        return new SupplierLogisticsSnapshot
        {
            OrderId = snapshot.OrderId,
            SupplierId = snapshot.SupplierId,
            DeliveryId = snapshot.DeliveryId,
            CarrierCode = snapshot.CarrierCode,
            CarrierName = snapshot.CarrierName,
            TrackingNo = snapshot.TrackingNo,
            PackageTemperature = snapshot.PackageTemperature,
            StatusCode = LogisticsStatusCodes.Exception,
            ShippedAt = snapshot.ShippedAt,
            EstimatedArrivalAt = snapshot.EstimatedArrivalAt,
            DeliveredAt = snapshot.DeliveredAt,
            HasException = true,
            ExceptionMessage = _options.DelayDescription,
            DataSource = LogisticsDataSources.Fallback,
            Events = snapshot.Events.Append(new LogisticsTrackingEventSnapshot
            {
                EventId = Guid.NewGuid().ToString("N"),
                StatusCode = LogisticsStatusCodes.Exception,
                Location = snapshot.Events.LastOrDefault()?.Location ?? _options.OriginLocation,
                Description = _options.DelayDescription,
                OccurredAt = detectedAt
            }).ToList()
        };
    }

    private LogisticsTrackingEventSnapshot CreateEvent(
        string status,
        string location,
        string description,
        DateTime occurredAt,
        decimal? temperature,
        string packageTemperature,
        string? eventId = null) => new()
    {
        EventId = string.IsNullOrWhiteSpace(eventId)
            ? GroupBIds.NewId()
            : eventId,
        StatusCode = status,
        Location = location,
        Description = description,
        OccurredAt = occurredAt,
        TemperatureCelsius = temperature,
        IsTemperatureException = IsTemperatureException(packageTemperature, temperature)
    };

    private bool IsTemperatureException(string packageTemperature, decimal? temperature)
    {
        if (!temperature.HasValue) return false;

        return packageTemperature.Trim().ToUpperInvariant() switch
        {
            "FROZEN" => temperature.Value > _options.FrozenMaximumCelsius,
            _ => temperature.Value < _options.ChilledMinimumCelsius ||
                 temperature.Value > _options.ChilledMaximumCelsius
        };
    }

    private static string CreateKey(string orderId, string supplierId) =>
        $"{orderId}|{supplierId}";

    private static string? ValueOrDefault(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
