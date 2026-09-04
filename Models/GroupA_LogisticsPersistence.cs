namespace FreshColdChain.Models;

/// <summary>A 组发货扩展元数据；与基础发货单一对一。</summary>
public sealed class LogLogisticsDetail
{
    public string DeliveryId { get; set; } = string.Empty;
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNo { get; set; }
    public string PackageTemperature { get; set; } = "CHILLED";
    public DateTime? EstimatedArrivalAt { get; set; }
    public string? CarrierTrackingKey { get; set; }
    public string? Remark { get; set; }
}

/// <summary>A 组不可变物流事件，原请求哈希用于检查幂等载荷冲突。</summary>
public sealed class LogLogisticsEvent
{
    public string EventId { get; set; } = string.Empty;
    public string DeliveryId { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public string StatusCode { get; set; } = LogisticsStatusCodes.Shipped;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public int IsTemperatureException { get; set; }
}

public sealed class GroupALogisticsOptions
{
    public const string SectionName = "GroupA:Logistics";
    public string Provider { get; set; } = "Oracle";
    public decimal ChilledMinimumCelsius { get; set; } = 0;
    public decimal ChilledMaximumCelsius { get; set; } = 8;
    public decimal FrozenMaximumCelsius { get; set; } = -18;
}

/// <summary>数据库唯一约束冲突，由业务层转换成可向调用方显示的错误。</summary>
public sealed class LogisticsWriteConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
