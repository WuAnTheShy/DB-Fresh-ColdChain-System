namespace FreshColdChain.Models.DTOs;

public class SupplierDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public int ProductCount { get; set; }
}

public class CreateSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
    public string? LoginPassword { get; set; }
}

/// <summary>
/// 供应商的某个产品及其供货价（未报价时 SupplyPrice 为 null）
/// </summary>
public class SupplierProductQuoteDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? SupplyPrice { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>
/// 跨组查询用 — C 组通过此 DTO 获取供应商账户信息（不含密码）
/// </summary>
public class SupplierAccountDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? LicenseNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int CreditLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginAccount { get; set; }
}
