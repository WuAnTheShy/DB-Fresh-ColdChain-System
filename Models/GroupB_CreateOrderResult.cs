namespace FreshColdChain.Models;

/// <summary>
/// 创建订单结果。
/// </summary>
public sealed class CreateOrderResult
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public decimal GoodsAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public int PointsEarned { get; init; }
    public IReadOnlyList<SupplierOrderGroupResult> SupplierGroups { get; init; } = [];
}

/// <summary>
/// 按供应商形成的订单明细分组。
/// </summary>
public sealed class SupplierOrderGroupResult
{
    public string SupplierId { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public IReadOnlyList<OrderItemResult> Items { get; init; } = [];
}

/// <summary>
/// 服务端生成的订单商品快照。
/// </summary>
public sealed class OrderItemResult
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}
