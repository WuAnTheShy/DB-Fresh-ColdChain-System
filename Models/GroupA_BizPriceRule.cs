using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 动态定价规则（文档表名：Biz_PriceRules）
/// 支持四种触发类型：
///   TimeBased      — 时段折扣（解析 TimeWindow）
///   ExpiryApproaching — 临期折扣（关联批次过期时间）
///   BulkDiscount   — 批量折扣（数量 >= MinQuantity）
///   ManualPrice    — 手动调价（直接设定价格，不使用折扣率）
/// 匹配逻辑：Priority 越小越优先，取第一个满足触发条件的规则
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

    /// <summary>归属供应商：该规则只适用于此供应商的货物（每家有自己一套规则）</summary>
    [Column("SupplierID")]
    [MaxLength(36)]
    public string? SupplierID { get; set; }

    [Column("RuleName")]
    [MaxLength(100)]
    public string? RuleName { get; set; }

    /// <summary>触发类型：TimeBased / ExpiryApproaching / BulkDiscount / ManualPrice</summary>
    [Column("TriggerType")]
    [MaxLength(50)]
    public string? TriggerType { get; set; }

    /// <summary>时段窗口（如 "18:00-20:00"），仅 TimeBased 类型使用</summary>
    [Column("TimeWindow")]
    [MaxLength(50)]
    public string? TimeWindow { get; set; }

    /// <summary>折后价格占比（0.8=8折=原价×0.8，0.95=95折），ManualPrice 类型可为 null</summary>
    [Column("DiscountRate")]
    public decimal? DiscountRate { get; set; }

    /// <summary>手动定价金额（仅 ManualPrice 类型使用）</summary>
    [Column("ManualPrice")]
    public decimal? ManualPrice { get; set; }

    /// <summary>最低购买数量（BulkDiscount 类型使用）</summary>
    [Column("MinQuantity")]
    public decimal? MinQuantity { get; set; }

    /// <summary>最高购买数量（可选）</summary>
    [Column("MaxQuantity")]
    public decimal? MaxQuantity { get; set; }

    /// <summary>优先级：数字越小越优先，默认 100</summary>
    [Column("Priority")]
    public int Priority { get; set; } = 100;

    /// <summary>是否启用：1=启用，0=禁用</summary>
    [Column("IsActive")]
    public int IsActive { get; set; } = 1;

    /// <summary>规则生效开始时间（可选）</summary>
    [Column("EffectiveFrom")]
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>规则生效结束时间（可选）</summary>
    [Column("EffectiveTo")]
    public DateTime? EffectiveTo { get; set; }

    // 导航属性
    [NotMapped] public InvProduct? Product { get; set; }
}
