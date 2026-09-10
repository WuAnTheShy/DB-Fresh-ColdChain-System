namespace FreshColdChain.Models.DTOs;

// An available promoter returned by the list endpoint.
public class GroupC_AvailablePromoterDto
{
    public string PromoterId { get; set; } = string.Empty;
    public string PromoterName { get; set; } = string.Empty;
}

// Basic promoter account information.
public class GroupC_PromoterBasicInfoDto
{
    public string PromoterId { get; set; } = string.Empty;
    public string PromoterName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // 团长预制头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn），未设置为空
    public string? Avatar { get; set; }

    // 团长头像图片地址（可直接用于 img src），未设置为空
    public string? AvatarUrl { get; set; }
}

// 团长带货商品（消费者端查看团长带货接口返回）：
// 含商品文字介绍（团长的文字，缺省时兜底供应商文字）、售价、商品图片等。
public class GroupC_FeaturedProductDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    // 商品加入团长带货列表的时间
    public DateTime PublishedAt { get; set; }

    // 商品计量单位（如：斤 / 盒 / 箱）
    public string? Unit { get; set; }

    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;

    // 供应商供货价
    public decimal SupplyPrice { get; set; }

    // 商品推荐价（参考售价）
    public decimal DefaultPrice { get; set; }

    // 消费者看到的售价：团长定价（未定价时为商品推荐价）
    public decimal Price { get; set; }

    // 团长带货介绍存储值：空=无介绍；/uploads/promoter-desc/*.json=图文内容文件相对路径；
    // 其它非空串=历史纯文字介绍（兼容，由读取端包装为单段文字）；
    // 消费者端目录接口在团长未填写介绍时可能兜底为供应商商品文字。
    public string PromoterDesc { get; set; } = string.Empty;

    // 商品图片（按展示顺序，最多 3 张）
    public List<string> Images { get; set; } = new();
}

// Paged response used by Group C query endpoints.
public class GroupC_PagedResult<T>
{

    public IEnumerable<GroupC_AvailablePromoterDto> Items { get; set; } = Enumerable.Empty<GroupC_AvailablePromoterDto>();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int Pageindex { get; set; }

}

// Query parameters for available promoters.
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
