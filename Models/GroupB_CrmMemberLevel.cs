namespace FreshColdChain.Models;

// Crm_MemberLevels - 会员等级定义
public class CrmMemberLevel
{
    public string MemberLevelId { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;   // 如: 普通/银卡/金卡/钻石
    public decimal MinSpent { get; set; }                    // 该等级最低消费门槛
    public decimal DiscountRate { get; set; }                // 折扣率 0.95 = 95折
    public int PointsMultiplier { get; set; }                // 积分倍率 1=1倍 2=双倍
}
