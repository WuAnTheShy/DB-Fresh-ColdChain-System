using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

// A 组数据库物流实现。基础发货单、承运商扩展信息与事件均参与调用方事务，
// 不持有进程内业务缓存。原一对一表 Log_LogisticsDetails 已并入 Log_ExpressDeliveries，
// 因此扩展信息直接读写基础发货单行。
public sealed class OracleGroupALogisticsExtensionProvider(IGroupALogisticsRepository repository,
    IOptions<GroupALogisticsOptions> options) : IGroupALogisticsExtensionProvider
{
    public async Task<SupplierLogisticsSnapshot> RegisterShipmentAsync(LogisticsShipmentRegistration registration,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ValidateTransaction(transaction);
        ValidateIds(registration.OrderId, registration.SupplierId);
        ValidateCommand(registration.Command);
        if (registration.SupplierId != registration.Command.SupplierId)
            throw new OrderBusinessException("发货登记供应商不一致");
        var delivery = await repository.GetDeliveryAsync(registration.OrderId, registration.SupplierId,
            true, transaction, cancellationToken) ?? throw new OrderBusinessException("基础发货单不存在");
        if (delivery.DeliveryID != registration.DeliveryId)
            throw new OrderBusinessException("发货登记与基础发货单不一致");
        var (hasDetail, events, persistedCount) = await LoadAsync(delivery, transaction, cancellationToken);
        if (hasDetail) return Snapshot(delivery, WithDelay(delivery, events));

        var command = registration.Command;
        var zone = command.PackageTemperature.Trim().ToUpperInvariant();
        if (zone is not ("CHILLED" or "FROZEN" or "AMBIENT"))
            throw new OrderBusinessException("运输温区无效");
        if (command.EstimatedArrivalAt.HasValue && LocalTime(command.EstimatedArrivalAt.Value) <= delivery.ShippedAt)
            throw new OrderBusinessException("预计送达时间必须晚于发货时间");
        var carrierCode = Clean(command.CarrierCode);
        var carrierName = Clean(command.CarrierName) ?? Clean(delivery.CarrierName);
        var trackingNo = Clean(command.TrackingNo) ?? delivery.TrackingNo;
        if (!string.IsNullOrWhiteSpace(command.TrackingNo) && carrierCode == null && carrierName == null)
            throw new OrderBusinessException("填写外部运单号时必须同时填写承运商");
        delivery.CarrierCode = carrierCode;
        delivery.CarrierName = carrierName;
        delivery.TrackingNo = trackingNo;
        delivery.PackageTemp = zone;
        delivery.Remark = Clean(command.Remark);
        delivery.EstimatedArrivalAt = command.EstimatedArrivalAt.HasValue ? LocalTime(command.EstimatedArrivalAt.Value) : null;
        delivery.CarrierTrackingKey = (carrierCode ?? carrierName) is { } carrier && !string.IsNullOrWhiteSpace(trackingNo)
            ? Hash(JsonSerializer.Serialize(new[] { carrier.ToUpperInvariant(), trackingNo.ToUpperInvariant() })) : null;
        await PersistAsync(() => repository.UpdateDetailAsync(delivery, transaction, cancellationToken));
        foreach (var item in events.Skip(persistedCount))
            await PersistAsync(() => repository.InsertEventAsync(item, transaction, cancellationToken));
        return Snapshot(delivery, WithDelay(delivery, events));
    }

    public async Task<SupplierLogisticsSnapshot> GetSnapshotAsync(LogisticsTraceSeed seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ValidateIds(seed.OrderId, seed.SupplierId);
        var delivery = await repository.GetDeliveryAsync(seed.OrderId, seed.SupplierId, false,
            cancellationToken: cancellationToken);
        if (delivery == null)
            return new() { OrderId = seed.OrderId, SupplierId = seed.SupplierId, DataSource = LogisticsDataSources.GroupA };
        var (_, events, _) = await LoadAsync(delivery, null, cancellationToken);
        // 读取不落库。延误事件使用稳定编号和时间，下一次轨迹写入时随事务持久化。
        return Snapshot(delivery, WithDelay(delivery, events));
    }

    public async Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(LogisticsTrackingEventCommand command,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateTransaction(transaction);
        ValidateCommand(command);
        ValidateIds(command.OrderId, command.SupplierId);
        if (!GroupBIds.IsValid(command.EventId) || !LogisticsStatusCodes.IsSupported(command.StatusCode))
            throw new OrderBusinessException("物流事件编号或状态代码无效");
        var delivery = await repository.GetDeliveryAsync(command.OrderId, command.SupplierId, true,
            transaction, cancellationToken) ?? throw new OrderBusinessException("供应商尚未发货");
        var (hasDetail, loadedEvents, persistedCount) = await LoadAsync(delivery, transaction, cancellationToken);
        var occurredAt = LocalTime(command.OccurredAt);
        var requestedStatus = LogisticsStatusCodes.Normalize(command.StatusCode);
        var requestHash = Hash(JsonSerializer.Serialize(new
        {
            command.OrderId, command.SupplierId, StatusCode = requestedStatus,
            Location = command.Location?.Trim() ?? "", Description = command.Description.Trim(),
            OccurredAt = occurredAt,
            TemperatureCelsius = command.TemperatureCelsius?.ToString("G29", CultureInfo.InvariantCulture)
        }));
        var existing = await repository.GetEventAsync(command.EventId, transaction, cancellationToken);
        if (existing != null)
        {
            if (existing.DeliveryId != delivery.DeliveryID || existing.RequestHash != requestHash)
                throw new OrderBusinessException("同一物流事件编号不能使用不同内容或归属");
            return Snapshot(delivery, WithDelay(delivery, loadedEvents));
        }
        var events = WithDelay(delivery, loadedEvents);
        var current = events[^1];
        if (occurredAt < current.OccurredAt || occurredAt > DateTime.Now.AddMinutes(5))
            throw new OrderBusinessException("物流事件时间不能早于最新轨迹或晚于当前时间");
        LogisticsStateMachine.EnsureTransition(current.StatusCode, requestedStatus);
        var temperatureException = IsTemperatureException(delivery.PackageTemp, command.TemperatureCelsius);
        var effectiveStatus = temperatureException ? LogisticsStatusCodes.Exception : requestedStatus;
        LogisticsStateMachine.EnsureTransition(current.StatusCode, effectiveStatus);
        if (!hasDetail) await PersistAsync(() => repository.UpdateDetailAsync(delivery, transaction, cancellationToken));
        foreach (var item in events.Skip(persistedCount))
            await PersistAsync(() => repository.InsertEventAsync(item, transaction, cancellationToken));
        var next = new LogLogisticsEvent
        {
            EventId = command.EventId, DeliveryId = delivery.DeliveryID, RequestHash = requestHash,
            SequenceNo = events.Count, StatusCode = effectiveStatus, Location = command.Location?.Trim() ?? "",
            Description = command.Description.Trim(), OccurredAt = occurredAt,
            TemperatureCelsius = command.TemperatureCelsius, IsTemperatureException = temperatureException ? 1 : 0
        };
        await PersistAsync(() => repository.InsertEventAsync(next, transaction, cancellationToken));
        await repository.UpdateBaseStatusAsync(delivery.DeliveryID, effectiveStatus, transaction, cancellationToken);
        events.Add(next);
        return Snapshot(delivery, events);
    }

    private async Task<(bool HasDetail, List<LogLogisticsEvent> Events, int PersistedCount)>
        LoadAsync(LogExpressDelivery delivery, IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        var events = (await repository.GetEventsAsync(delivery.DeliveryID, transaction, cancellationToken)).ToList();
        var persistedCount = events.Count;
        if (events.Count == 0)
        {
            if (!LogisticsStatusCodes.IsShippedOrLater(delivery.LogisticsStatus))
                throw new OrderBusinessException("基础发货单状态无效");
            events.Add(new()
            {
                EventId = SeedId(delivery), DeliveryId = delivery.DeliveryID, SequenceNo = 0,
                RequestHash = Hash("基础发货记录:" + delivery.DeliveryID),
                StatusCode = LogisticsStatusCodes.Normalize(delivery.LogisticsStatus),
                Description = "基础发货记录（时间为原发货时间）", OccurredAt = LocalTime(delivery.ShippedAt)
            });
        }
        return (delivery.IsRegistered == 1, events, persistedCount);
    }

    private static List<LogLogisticsEvent> WithDelay(LogExpressDelivery delivery, List<LogLogisticsEvent> events)
    {
        var delayId = Hash("DELAY:" + delivery.DeliveryID)[..32];
        var current = events[^1];
        if (delivery.EstimatedArrivalAt is not { } expected || expected >= DateTime.Now ||
            current.StatusCode is LogisticsStatusCodes.Delivered or LogisticsStatusCodes.Returned or LogisticsStatusCodes.Exception ||
            events.Any(item => item.EventId == delayId)) return events;
        events.Add(new()
        {
            EventId = delayId, DeliveryId = delivery.DeliveryID, RequestHash = Hash("DELAY:" + delivery.DeliveryID),
            SequenceNo = events.Count, StatusCode = LogisticsStatusCodes.Exception,
            Location = current.Location, Description = "包裹已超过预计送达时间，请联系承运商核查",
            OccurredAt = expected > current.OccurredAt ? expected : current.OccurredAt
        });
        return events;
    }

    private static SupplierLogisticsSnapshot Snapshot(LogExpressDelivery delivery, IReadOnlyList<LogLogisticsEvent> events)
    {
        var latest = events[^1];
        return new()
        {
            DeliveryId = delivery.DeliveryID, OrderId = delivery.OrderID, SupplierId = delivery.SupplierID,
            CarrierCode = delivery.CarrierCode, CarrierName = delivery.CarrierName, TrackingNo = delivery.TrackingNo,
            PackageTemperature = delivery.PackageTemp, StatusCode = latest.StatusCode,
            ShippedAt = delivery.ShippedAt, EstimatedArrivalAt = delivery.EstimatedArrivalAt,
            DeliveredAt = events.FirstOrDefault(item => item.StatusCode == LogisticsStatusCodes.Delivered && item.EventId != SeedId(delivery))?.OccurredAt,
            HasException = latest.StatusCode == LogisticsStatusCodes.Exception,
            ExceptionMessage = latest.StatusCode != LogisticsStatusCodes.Exception ? null
                : latest.IsTemperatureException == 1 ? "运输温度超出商品温区范围" : latest.Description,
            DataSource = LogisticsDataSources.GroupA,
            Events = events.OrderBy(item => item.OccurredAt).ThenBy(item => item.SequenceNo).Select(item => new LogisticsTrackingEventSnapshot
            {
                EventId = item.EventId, StatusCode = item.StatusCode, Location = item.Location ?? "",
                Description = item.Description, OccurredAt = item.OccurredAt, TemperatureCelsius = item.TemperatureCelsius,
                IsTemperatureException = item.IsTemperatureException == 1
            }).ToList()
        };
    }

    private bool IsTemperatureException(string zone, decimal? temperature) => temperature.HasValue && zone.Trim().ToUpperInvariant() switch
    {
        "FROZEN" => temperature > options.Value.FrozenMaximumCelsius,
        "CHILLED" => temperature < options.Value.ChilledMinimumCelsius || temperature > options.Value.ChilledMaximumCelsius,
        _ => false
    };

    private static string SeedId(LogExpressDelivery delivery) => Hash("SHIP:" + delivery.DeliveryID)[..32];
    private static async Task PersistAsync(Func<Task> write)
    {
        try { await write(); }
        catch (LogisticsWriteConflictException exception) { throw new OrderBusinessException(exception.Message); }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime LocalTime(DateTime time) => DateTime.SpecifyKind(time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time, DateTimeKind.Unspecified);
    private static void ValidateIds(string orderId, string supplierId)
    {
        if (!GroupBIds.IsValid(orderId) || !GroupBIds.IsValid(supplierId))
            throw new OrderBusinessException("订单或供应商编号无效");
    }
    private static void ValidateTransaction(IDbTransaction transaction)
    {
        if (transaction?.Connection?.State != ConnectionState.Open)
            throw new OrderBusinessException("物流操作必须参加有效的调用方事务");
    }
    private static void ValidateCommand(object command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try { Validator.ValidateObject(command, new ValidationContext(command), true); }
        catch (ValidationException exception) { throw new OrderBusinessException($"物流参数无效：{exception.Message}"); }
    }
}
