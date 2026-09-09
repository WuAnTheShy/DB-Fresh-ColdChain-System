namespace FreshColdChain.Models;

/// <summary>
/// 商品「跟团记录」行：某团长在团商品下，购买过该商品的消费者（按消费者聚合）。
/// </summary>
public sealed class ProductGroupRecord
{
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn）。</summary>
    public string? Avatar { get; init; }

    public string? SupplierId { get; init; }

    /// <summary>该消费者累计购得该商品数量。</summary>
    public int TotalQuantity { get; init; }

    /// <summary>该消费者累计实付金额。</summary>
    public decimal TotalSpent { get; init; }

    /// <summary>最近一次购买时间。</summary>
    public DateTime PurchasedAt { get; init; }

    public string OrderStatus { get; init; } = OrderStatusCodes.Paid;
}
