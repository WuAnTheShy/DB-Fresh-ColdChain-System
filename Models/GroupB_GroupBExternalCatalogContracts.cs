using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FreshColdChain.Models;

// B 组调用 A 组商品目录时使用的查询条件。
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

// A 组返回给 B 组后端的消费者可售商品。
public sealed class GroupAConsumerProduct
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? CategoryName { get; init; }
    public string? Unit { get; init; }
    public string? StorageRequirement { get; init; }
    public decimal SalePrice { get; init; }
    public bool IsInStock { get; init; }
    public int AvailableStock { get; init; }

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

// 结算和下单前由 A 组提供的可信商品信息。
public sealed class GroupATrustedProduct
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public decimal SalePrice { get; init; }
    public int AvailableStock { get; init; }
    public bool IsOnSale { get; init; }
    public string? CategoryName { get; init; }
    public string? Unit { get; init; }
    public string? StorageRequirement { get; init; }
}

// B 组调用 C 组团长目录时使用的查询条件。
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

// B 组后端提交给 C 组的团长带货关系校验项。
public sealed class GroupCPromoterProductCandidate
{
    public string ProductId { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
}

public sealed class GroupCPromoterProductValidation
{
    public string ProductId { get; init; } = string.Empty;

    // 供货供应商 ID：同一商品不同供应商分别校验在团与报价。
    public string SupplierId { get; init; } = string.Empty;

    public bool IsAllowed { get; init; }
    public decimal? SalePrice { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
}

// C 组返回给 B 组后端的团长在团商品。
public sealed class GroupCPromoterFeaturedProduct
{
    public string ProductId { get; init; } = string.Empty;

    // 仅供 B 组后端与 A 组商品来源交叉校验，禁止序列化给消费者前端。
    [JsonIgnore]
    public string SupplierId { get; init; } = string.Empty;

    // 供货供应商名称，消费者端用于区分同一商品的不同供货来源。
    [JsonIgnore]
    public string? SupplierName { get; init; }

    public decimal SalePrice { get; init; }
    public DateTime PublishedAt { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
}

// B 组消费者端使用的真实在团商品，只包含可公开字段。
public sealed class ConsumerCatalogProduct
{
    public string CatalogItemId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string? StorageRequirement { get; init; }

    // 供货供应商 ID。同一商品可能由多个供应商分别上架，供消费者端区分。
    public string SupplierId { get; init; } = string.Empty;

    // 供货供应商名称，消费者端展示用。
    public string? SupplierName { get; init; }

    public decimal SalePrice { get; init; }
    public int AvailableStock { get; init; }
    public string PromoterId { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
}

public sealed class ConsumerCatalogResult
{
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<ConsumerCatalogProduct> Products { get; init; } = [];
}
