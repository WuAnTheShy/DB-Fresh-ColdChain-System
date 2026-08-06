namespace FreshColdChain.Models.DTOs;

// ========== 价格计算 ==========

/// <summary>价格计算请求 — B 组下单时计算实时价格</summary>
public class PriceCalculationRequest
{
    public string ProductID { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    /// <summary>可选：当前时间（默认 DateTime.Now），便于测试</summary>
    public DateTime? ReferenceTime { get; set; }
}

/// <summary>价格计算结果</summary>
public class PriceCalculationResult
{
    public string ProductID { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public decimal FinalPrice { get; set; }
    /// <summary>应用的规则名称（null 表示无规则命中，使用默认价格）</summary>
    public string? MatchedRuleName { get; set; }
    /// <summary>应用的规则 ID</summary>
    public string? MatchedRuleID { get; set; }
    /// <summary>触发类型</summary>
    public string? TriggerType { get; set; }
    /// <summary>折扣金额（DefaultPrice - FinalPrice）</summary>
    public decimal DiscountAmount => DefaultPrice - FinalPrice;
}

// ========== 规则管理 DTO ==========

/// <summary>价格规则展示/列表</summary>
public class PriceRuleDto
{
    public string RuleID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? RuleName { get; set; }
    public string? TriggerType { get; set; }
    public string? TimeWindow { get; set; }
    public decimal? DiscountRate { get; set; }
    public decimal? ManualPrice { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public int Priority { get; set; }
    public int IsActive { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>创建/编辑价格规则请求</summary>
public class SavePriceRuleDto
{
    public string ProductID { get; set; } = string.Empty;
    public string? RuleName { get; set; }
    public string? TriggerType { get; set; }
    public string? TimeWindow { get; set; }
    public decimal? DiscountRate { get; set; }
    public decimal? ManualPrice { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public int Priority { get; set; } = 100;
    public int IsActive { get; set; } = 1;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
