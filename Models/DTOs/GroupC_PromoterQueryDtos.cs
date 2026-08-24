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
