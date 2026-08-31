namespace FreshColdChain.Models;

public sealed class CrmPromoter
{
    public int PromoterId { get; set; }
    public string PromoterName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Avatar { get; set; }  // 预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn）
    public decimal BaseCommissionRate { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalOrderCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}