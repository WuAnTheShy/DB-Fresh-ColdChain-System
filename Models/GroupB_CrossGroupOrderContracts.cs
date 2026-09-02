namespace FreshColdChain.Models;

/// <summary>
/// A 组冷链运费计算请求。
/// </summary>
public sealed class FreightCalculationRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = [];
}

/// <summary>A 组返回、B 组用于计价和审计的结构化运费结果。</summary>
public sealed class FreightCalculationResult
{
    public int SchemaVersion { get; init; } = 1;
    public decimal FreightAmount { get; init; }
    public decimal GoodsAmount { get; init; }
    public string Province { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public string RuleSummary { get; init; } = string.Empty;
    public DateTime CalculatedAt { get; init; }
    public string DataSource { get; init; } = LogisticsDataSources.GroupA;
    public IReadOnlyList<FreightCalculationItemResult> Items { get; init; } = [];
}

public sealed class FreightCalculationItemResult
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}

/// <summary>
/// A 组发货或释放库存所需的订单快照。
/// </summary>
public sealed class FulfillmentOrderRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = [];
}

/// <summary>
/// 跨组传递的最小订单商品快照。
/// </summary>
public sealed class FulfillmentOrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}

/// <summary>
/// A 组返回的供应商履约状态，不在 B 组重复持久化物流表。
/// </summary>
public sealed class SupplierFulfillmentStatus
{
    public string SupplierId { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string? TrackingNo { get; init; }
}

