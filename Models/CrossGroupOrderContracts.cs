namespace FreshColdChain.Models;

/// <summary>
/// A 组冷链运费计算请求。
/// </summary>
public sealed class FreightCalculationRequest
{
    public int CustomerId { get; init; }
    public string Province { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = [];
}

/// <summary>
/// A 组发货或释放库存所需的订单快照。
/// </summary>
public sealed class FulfillmentOrderRequest
{
    public int OrderId { get; init; }
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

/// <summary>
/// C 组在订单完成时计算预计佣金所需的可信快照。
/// </summary>
public sealed class CommissionOrderRequest
{
    public int OrderId { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public string? PromoterId { get; init; }
    public decimal CommissionBaseAmount { get; init; }
    public DateTime CompletedAt { get; init; }
}
