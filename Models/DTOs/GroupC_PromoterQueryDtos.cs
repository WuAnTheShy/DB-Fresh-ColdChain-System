namespace FreshColdChain.Models.DTOs;

/// <summary>
/// An available promoter returned by the list endpoint.
/// </summary>
public class GroupC_AvailablePromoterDto
{
    public string PromoterId { get; set; } = string.Empty;
    public string PromoterName { get; set; } = string.Empty;
}

/// <summary>
/// Basic promoter account information.
/// </summary>
public class GroupC_PromoterBasicInfoDto
{
    public string PromoterId { get; set; } = string.Empty;
    public string PromoterName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>团长预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn），未设置为空</summary>
    public string? Avatar { get; set; }

    /// <summary>团长头像图片地址（可直接用于 img src），未设置为空</summary>
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// 团长带货商品（消费者端查看团长带货接口返回）：
/// 含商品文字介绍（团长的文字，缺省时兜底供应商文字）、售价、商品图片等。
/// </summary>
public class GroupC_FeaturedProductDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    /// <summary>商品计量单位（如：斤 / 盒 / 箱）</summary>
    public string? Unit { get; set; }

    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>供应商供货价</summary>
    public decimal SupplyPrice { get; set; }

    /// <summary>商品推荐价（参考售价）</summary>
    public decimal DefaultPrice { get; set; }

    /// <summary>消费者看到的售价：团长定价（未定价时为商品推荐价）</summary>
    public decimal Price { get; set; }

    /// <summary>团长带货介绍文字（给消费者端展示；缺省时兜底为供应商商品文字）</summary>
    public string PromoterDesc { get; set; } = string.Empty;

    /// <summary>商品图片（按展示顺序，最多 3 张）</summary>
    public List<string> Images { get; set; } = new();
}

/// <summary>
/// Paged response used by Group C query endpoints.
/// </summary>
public class GroupC_PagedResult<T>
{

    public IEnumerable<GroupC_AvailablePromoterDto> Items { get; set; } = Enumerable.Empty<GroupC_AvailablePromoterDto>();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int Pageindex { get; set; }

}

/// <summary>
/// Query parameters for available promoters.
/// </summary>
public class GroupC_AvailablePromoterQuery
{
    public string? Keyword { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

//团长查找列表类
public class GroupC_PromoterListResult
{
    public IEnumerable<GroupC_AvailablePromoterDto> Items { get; set; } = Enumerable.Empty<GroupC_AvailablePromoterDto>();
    public int TotalCount { get; set; }

}
