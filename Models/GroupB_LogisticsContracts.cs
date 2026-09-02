using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>A 组物流状态在 B 组履约编排中的稳定代码。</summary>
public static class LogisticsStatusCodes
{
    public const string Pending = "PENDING";
    public const string Packing = "PACKING";
    public const string Shipped = "SHIPPED";
    public const string InTransit = "IN_TRANSIT";
    public const string OutForDelivery = "OUT_FOR_DELIVERY";
    public const string Delivered = "DELIVERED";
    public const string Exception = "EXCEPTION";
    public const string Returning = "RETURNING";
    public const string Returned = "RETURNED";

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Pending;

        return status.Trim().ToUpperInvariant() switch
        {
            "PENDING" or "待发货" => Pending,
            "PACKING" or "备货中" => Packing,
            "SHIPPED" or "已发货" => Shipped,
            "IN_TRANSIT" or "运输中" => InTransit,
            "OUT_FOR_DELIVERY" or "派送中" => OutForDelivery,
            "DELIVERED" or "已签收" => Delivered,
            "EXCEPTION" or "异常" => Exception,
            "RETURNING" or "退回中" => Returning,
            "RETURNED" or "已退回" => Returned,
            _ => Pending
        };
    }

    public static string GetName(string? status) => Normalize(status) switch
    {
        Pending => "待发货",
        Packing => "备货中",
        Shipped => "已发货",
        InTransit => "运输中",
        OutForDelivery => "派送中",
        Delivered => "已签收",
        Exception => "物流异常",
        Returning => "退回中",
        Returned => "已退回",
        _ => "待发货"
    };

    public static bool IsShippedOrLater(string? status) => Normalize(status) is
        Shipped or InTransit or OutForDelivery or Delivered or Exception or Returning or Returned;
}

public static class LogisticsDataSources
{
    public const string GroupA = "GROUP_A";
    public const string Fallback = "FALLBACK";
}

/// <summary>供应商发货时由 B 组传给 A 组的履约补充信息。</summary>
public sealed class SupplierShipmentCommand
{
    [Required, StringLength(36)]
    public string SupplierId { get; init; } = string.Empty;

    [StringLength(30)]
    public string? CarrierCode { get; init; }

    [StringLength(100)]
    public string? CarrierName { get; init; }

    [StringLength(100)]
    public string? TrackingNo { get; init; }

    [Required, StringLength(20)]
    public string PackageTemperature { get; init; } = "CHILLED";

    public DateTime? EstimatedArrivalAt { get; init; }

    [StringLength(300)]
    public string? Remark { get; init; }
}

/// <summary>跨组登记发货扩展信息时使用的可信快照。</summary>
public sealed class LogisticsShipmentRegistration
{
    public string DeliveryId { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public string BaseTrackingNo { get; init; } = string.Empty;
    public string BaseStatus { get; init; } = LogisticsStatusCodes.Shipped;
    public DateTime ShippedAt { get; init; }
    public SupplierShipmentCommand Command { get; init; } = new();
}

/// <summary>A 组基础物流记录与扩展 Provider 组合时的输入。</summary>
public sealed class LogisticsTraceSeed
{
    public string OrderId { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public string? DeliveryId { get; init; }
    public string? TrackingNo { get; init; }
    public string StatusCode { get; init; } = LogisticsStatusCodes.Pending;
    public DateTime? ShippedAt { get; init; }
}

/// <summary>标准化物流轨迹事件。</summary>
public sealed class LogisticsTrackingEventSnapshot
{
    public string EventId { get; init; } = string.Empty;
    public string StatusCode { get; init; } = LogisticsStatusCodes.Pending;
    public string StatusName => LogisticsStatusCodes.GetName(StatusCode);
    public string Location { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public bool IsTemperatureException { get; init; }
}

/// <summary>供应商级完整履约快照；B 组不重复持久化 A 组物流表。</summary>
public sealed class SupplierLogisticsSnapshot
{
    public string OrderId { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public string? DeliveryId { get; init; }
    public string? CarrierCode { get; init; }
    public string? CarrierName { get; init; }
    public string? TrackingNo { get; init; }
    public string PackageTemperature { get; init; } = "CHILLED";
    public string StatusCode { get; init; } = LogisticsStatusCodes.Pending;
    public string StatusName => LogisticsStatusCodes.GetName(StatusCode);
    public DateTime? ShippedAt { get; init; }
    public DateTime? EstimatedArrivalAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public bool HasException { get; init; }
    public string? ExceptionMessage { get; init; }
    public string DataSource { get; init; } = LogisticsDataSources.GroupA;
    public bool IsFallback => DataSource == LogisticsDataSources.Fallback;
    public IReadOnlyList<LogisticsTrackingEventSnapshot> Events { get; init; } = [];
}

/// <summary>新增轨迹事件命令；未来由 A 组真实接口持久化。</summary>
public sealed class LogisticsTrackingEventCommand
{
    [Required, StringLength(36)]
    public string OrderId { get; init; } = string.Empty;

    [Required, StringLength(36)]
    public string SupplierId { get; init; } = string.Empty;

    [Required, StringLength(30)]
    public string StatusCode { get; init; } = LogisticsStatusCodes.InTransit;

    [StringLength(200)]
    public string Location { get; init; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; } = DateTime.Now;
    public decimal? TemperatureCelsius { get; init; }
}

/// <summary>配置化兜底 Provider 的运行参数。</summary>
public sealed class GroupALogisticsFallbackOptions
{
    public const string SectionName = "GroupB:LogisticsFallback";

    public string CarrierCode { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string OriginLocation { get; set; } = string.Empty;
    public string ShippedDescription { get; set; } = string.Empty;
    public int EstimatedTransitHours { get; set; }
    public decimal ChilledMinimumCelsius { get; set; }
    public decimal ChilledMaximumCelsius { get; set; }
    public decimal FrozenMaximumCelsius { get; set; }
}
