namespace FreshGroupSystem.Models.DTOs;

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
