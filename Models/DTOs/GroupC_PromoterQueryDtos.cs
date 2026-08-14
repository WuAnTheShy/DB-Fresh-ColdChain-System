namespace DBFreshColdChain.Models.DTOs;

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
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public List<T> Items { get; set; } = new();
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
