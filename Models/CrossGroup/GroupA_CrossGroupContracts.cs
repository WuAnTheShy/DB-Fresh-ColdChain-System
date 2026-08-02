namespace FreshColdChain.Models.CrossGroup;

// ========== A 组库存模块契约模型 ==========

/// <summary>B 组向 A 组提交的库存预留请求项</summary>
public class InventoryReservationItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

/// <summary>A 组返回的可信商品快照</summary>
public class InventoryProductSnapshot
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
}

// ========== A 组物流模块契约模型 ==========

/// <summary>运费计算请求</summary>
public class FreightCalculationRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = new List<FulfillmentOrderItem>();
}

/// <summary>发货/释放库存的订单快照</summary>
public class FulfillmentOrderRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = new List<FulfillmentOrderItem>();
}

/// <summary>跨组传递的商品快照</summary>
public class FulfillmentOrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}

/// <summary>A 组返回的供应商履约状态</summary>
public class SupplierFulfillmentStatus
{
    public string SupplierId { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string? TrackingNo { get; init; }
}

// ========== C 组佣金模块契约模型 ==========

/// <summary>订单完成时传给 C 组计算佣金</summary>
public class CommissionOrderRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? PromoterId { get; init; }
    public decimal CommissionBaseAmount { get; init; }
    public DateTime CompletedAt { get; init; }
}
