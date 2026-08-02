using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 动态定价规则（文档表名：Biz_PriceRules）
/// </summary>
[Table("Biz_PriceRules")]
public class BizPriceRule
{
    [Key]
    [Column("RuleID")]
    [MaxLength(36)]
    public string RuleID { get; set; } = Guid.NewGuid().ToString();

    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("RuleName")]
    [MaxLength(100)]
    public string? RuleName { get; set; }

    [Column("TimeWindow")]
    [MaxLength(50)]
    public string? TimeWindow { get; set; }

    [Column("DiscountRate")]
    public decimal? DiscountRate { get; set; }

    [Column("TriggerType")]
    [MaxLength(50)]
    public string? TriggerType { get; set; }

    public InvProduct? Product { get; set; }
}
