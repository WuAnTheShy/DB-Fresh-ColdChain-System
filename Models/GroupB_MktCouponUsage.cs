namespace FreshColdChain.Models;

/// <summary>
/// 下单事务中锁定的优惠券使用信息。
/// </summary>
public sealed class MktCouponUsage
{
    public string RecordId { get; set; } = string.Empty;
    public string CouponId { get; set; } = string.Empty;
    public string CouponName { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
}
