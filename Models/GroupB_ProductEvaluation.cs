using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

public static class ProductEvaluationDimensions
{
    public const string HighQuality = "HIGH_QUALITY";
    public const string FastShipping = "FAST_SHIPPING";
    public const string GoodPackaging = "GOOD_PACKAGING";
    public const string CostEffective = "COST_EFFECTIVE";
    public const string Affordable = "AFFORDABLE";
    public const string ReliablePromoter = "RELIABLE_PROMOTER";

    public static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HighQuality] = "高品质",
            [FastShipping] = "发货快",
            [GoodPackaging] = "包装完好",
            [CostEffective] = "性价比高",
            [Affordable] = "价格实惠",
            [ReliablePromoter] = "团长靠谱"
        };
}

public sealed class ProductEvaluationRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "请至少选择一项评价")]
    public List<string> Dimensions { get; set; } = [];
}

public sealed class ProductEvaluation
{
    public string EvaluationId { get; set; } = string.Empty;
    public string OrderDetailId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string PromoterId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public int HighQuality { get; set; }
    public int FastShipping { get; set; }
    public int GoodPackaging { get; set; }
    public int CostEffective { get; set; }
    public int Affordable { get; set; }
    public int ReliablePromoter { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ProductEvaluationAggregate
{
    public int TotalCount { get; set; }
    public int HighQualityCount { get; set; }
    public int FastShippingCount { get; set; }
    public int GoodPackagingCount { get; set; }
    public int CostEffectiveCount { get; set; }
    public int AffordableCount { get; set; }
    public int ReliablePromoterCount { get; set; }
}

public sealed record ProductEvaluationDimensionSummary(string Code, string Name, int Count);

public sealed class ProductEvaluationSummary
{
    public int TotalCount { get; init; }
    public IReadOnlyList<ProductEvaluationDimensionSummary> Dimensions { get; init; } = [];
}
