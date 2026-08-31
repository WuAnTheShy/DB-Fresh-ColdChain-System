namespace FreshColdChain.Models;

/// <summary>消费者每月定级留痕。</summary>
public sealed class CrmMemberLevelHistory
{
    public string HistoryId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string MemberLevelId { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public decimal QualifiedSpent { get; set; }
    public DateTime SettlementMonth { get; set; }
    public DateTime CreatedAt { get; set; }
}
