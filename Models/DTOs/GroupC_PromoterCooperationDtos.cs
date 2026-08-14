namespace DBFreshColdChain.Models.DTOs;

public class GroupC_PromoterCooperationRangeResponse
{
    public string PromoterId { get; set; } = string.Empty;
    public List<string> InternalSupplierIds { get; set; } = new();
}

public class GroupC_PromoterProductSupplierItem
{
    public string ProductId { get; set; } = string.Empty;
    public string InternalSupplierId { get; set; } = string.Empty;
}

public class GroupC_PromoterProductCooperationVerificationRequest
{
    public string PromoterId { get; set; } = string.Empty;
    public List<GroupC_PromoterProductSupplierItem> Products { get; set; } = new();
}

public class GroupC_PromoterProductCooperationVerificationResult
{
    public string ProductId { get; set; } = string.Empty;
    public string InternalSupplierId { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}
