namespace FreshColdChain.Models;

/// <summary>
/// Crm_MemberLevels - 会员等级定义
/// </summary>
public class CrmMemberLevel
{
    public int MemberLevelId { get; set; }
    public string LevelName { get; set; } = string.Empty;   // 如: 普通/银卡/金卡/钻石
    public decimal MinSpent { get; set; }                    // 该等级最低消费门槛
    public decimal DiscountRate { get; set; }                // 折扣率 0.95 = 95折
    public int PointsMultiplier { get; set; }                // 积分倍率 1=1倍 2=双倍
}
