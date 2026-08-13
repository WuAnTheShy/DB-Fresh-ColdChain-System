namespace FreshColdChain.Models;

/// <summary>
/// Mkt_CouponRecords - 用户领券/用券记录
/// </summary>
public class MktCouponRecord
{
    public string RecordId { get; set; } = string.Empty;
    public string CouponId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? OrderId { get; set; }             // 核销时关联的订单ID
    public int Status { get; set; }                  // 0=未使用 1=已使用 2=已过期
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
