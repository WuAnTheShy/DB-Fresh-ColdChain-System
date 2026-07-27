namespace FreshColdChain.Models;

/// <summary>
/// Mkt_CouponRecords - 用户领券/用券记录
/// </summary>
public class MktCouponRecord
{
    public int RecordId { get; set; }
    public int CouponId { get; set; }
    public int CustomerId { get; set; }
    public int? OrderId { get; set; }               // 核销时关联的订单ID
    public int Status { get; set; }                  // 0=未使用 1=已使用 2=已过期
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
