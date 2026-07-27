namespace FreshColdChain.Models;

/// <summary>
/// 优惠券中心页面数据。
/// </summary>
public sealed class CouponCenterViewModel
{
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public IReadOnlyList<ClaimableCouponItem> ClaimableCoupons { get; init; } = [];
    public IReadOnlyList<AvailableCouponItem> AvailableCoupons { get; init; } = [];
}

/// <summary>
/// 可领取的券模板及当前消费者领取状态。
/// </summary>
public sealed class ClaimableCouponItem
{
    public int CouponId { get; init; }
    public string CouponName { get; init; } = string.Empty;
    public decimal MinOrderAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public int RemainingQuantity { get; init; }
    public DateTime EndTime { get; init; }
    public int HasClaimed { get; init; }
}

/// <summary>
/// 当前消费者已领取且仍可使用的优惠券。
/// </summary>
public sealed class AvailableCouponItem
{
    public int RecordId { get; init; }
    public int CouponId { get; init; }
    public string CouponName { get; init; } = string.Empty;
    public decimal MinOrderAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public DateTime EndTime { get; init; }
}
