namespace FreshColdChain.Models;

/// <summary>
/// Mkt_Coupons - 优惠券模板
/// </summary>
public class MktCoupon
{
    public int CouponId { get; set; }
    public string CouponName { get; set; } = string.Empty;
    public decimal MinOrderAmount { get; set; }     // 最低使用门槛
    public decimal DiscountAmount { get; set; }      // 优惠金额
    public int TotalQuantity { get; set; }           // 发行总量
    public int RemainingQuantity { get; set; }       // 剩余可领数量
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Status { get; set; }                  // 0=停用 1=启用
}
