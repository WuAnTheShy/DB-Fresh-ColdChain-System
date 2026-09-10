namespace FreshColdChain.Models.CrossGroup;

// A 组库存模块契约模型

// B 组向 A 组提交的库存预留请求项
public class InventoryReservationItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

// A 组返回的可信商品快照
public class InventoryProductSnapshot
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
}

// A 组物流模块契约模型

// 运费计算请求
public class FreightCalculationRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = new List<FulfillmentOrderItem>();
}

// 发货/释放库存的订单快照
public class FulfillmentOrderRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public IReadOnlyList<FulfillmentOrderItem> Items { get; init; } = new List<FulfillmentOrderItem>();
}

// 跨组传递的商品快照
public class FulfillmentOrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}

// A 组返回的供应商履约状态
public class SupplierFulfillmentStatus
{
    public string SupplierId { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string? TrackingNo { get; init; }
}


