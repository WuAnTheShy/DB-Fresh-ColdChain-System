using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FreshColdChain.Models;

/// <summary>B 组调用 A 组商品目录时使用的查询条件。</summary>
public sealed class GroupAProductSearchRequest
{
    public IReadOnlyList<string> SupplierIds { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Category { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 50)]
    public int PageSize { get; init; } = 20;
}

/// <summary>A 组返回给 B 组后端的消费者可售商品。</summary>
public sealed class GroupAConsumerProduct
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public decimal SalePrice { get; init; }
    public bool IsInStock { get; init; }

    // 仅供 B 组后端校验合作范围，禁止序列化给消费者前端。
    [JsonIgnore]
    public string SupplierId { get; init; } = string.Empty;
}

public sealed class GroupAProductSearchResult
{
    public IReadOnlyList<GroupAConsumerProduct> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>结算和下单前由 A 组提供的可信商品信息。</summary>
public sealed class GroupATrustedProduct
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public decimal SalePrice { get; init; }
    public int AvailableStock { get; init; }
    public bool IsOnSale { get; init; }
}

/// <summary>B 组调用 C 组团长目录时使用的查询条件。</summary>
public sealed class GroupCPromoterSearchRequest
{
    public string? Keyword { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 50)]
    public int PageSize { get; init; } = 20;
}

public sealed class GroupCPromoterSummary
{
    public string PromoterId { get; init; } = string.Empty;
    public string PromoterName { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
}

public sealed class GroupCPromoterSearchResult
{
    public IReadOnlyList<GroupCPromoterSummary> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>B 组后端提交给 C 组的团长带货关系校验项。</summary>
public sealed class GroupCPromoterProductCandidate
{
    public string ProductId { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
}

public sealed class GroupCPromoterProductValidation
{
    public string ProductId { get; init; } = string.Empty;
    public bool IsAllowed { get; init; }
}
