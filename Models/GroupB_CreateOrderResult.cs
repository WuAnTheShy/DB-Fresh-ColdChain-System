namespace FreshColdChain.Models;

// 创建订单结果。
public sealed class CreateOrderResult
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public FreightCalculationResult? FreightQuote { get; init; }
    public decimal FinalAmount { get; init; }
    public int PointsEarned { get; init; }
    public int PointsUsed { get; init; }
    public decimal PointsDiscountAmount { get; init; }
    public IReadOnlyList<SupplierOrderGroupResult> SupplierGroups { get; init; } = [];
}

// 消费者一次结算批次的拆单结果。
public sealed class CreateCheckoutBatchResult
{
    public string CheckoutBatchId { get; init; } = string.Empty;
    public DateTime PaymentExpiresAt { get; init; }
    public decimal GoodsAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public int PointsUsed { get; init; }
    public decimal PointsDiscountAmount { get; init; }
    public IReadOnlyList<CreateOrderResult> Orders { get; init; } = [];
    public IReadOnlyList<OrderPriceChangeResult> PriceChanges { get; init; } = [];
    public IReadOnlyList<AppliedCouponResult> AppliedCoupons { get; init; } = [];
}

public sealed class AppliedCouponResult
{
    public string RecordId { get; init; } = string.Empty;
    public string CouponName { get; init; } = string.Empty;
    public string CouponType { get; init; } = "NORMAL";
    public decimal DiscountAmount { get; init; }
    public bool WasAutoClaimed { get; init; }
}

public sealed class OrderPriceChangeResult
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal PreviousPrice { get; init; }
    public decimal LatestPrice { get; init; }
}

// 按供应商形成的订单明细分组。
public sealed class SupplierOrderGroupResult
{
    public string SupplierId { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public IReadOnlyList<OrderItemResult> Items { get; init; } = [];
}

// 服务端生成的订单商品快照。
public sealed class OrderItemResult
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}
