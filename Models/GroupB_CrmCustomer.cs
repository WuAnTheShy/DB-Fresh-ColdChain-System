namespace FreshColdChain.Models;

/// <summary>
/// Crm_Customers - 消费者
/// </summary>
public class CrmCustomer
{
    public string CustomerId { get; set; } = string.Empty;
    public string? OpenId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Avatar { get; set; }          // 预制头像标识
    public string PasswordHash { get; set; } = string.Empty;
    public string? PromoterId { get; set; }       // 所属团长ID（C组字符串GUID）
    public string? MemberLevelId { get; set; }     // 当前会员等级
    public decimal TotalSpent { get; set; }        // 累计消费金额(用于定级)
    public int Points { get; set; }                // 当前积分余额
    public int GrowthValue { get; set; }
    public DateTime? BindExpireTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
