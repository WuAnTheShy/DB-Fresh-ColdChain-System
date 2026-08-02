namespace FreshColdChain.Models.DTOs;

public class ProductDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? SupplierName { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public decimal DefaultPrice { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public int AvailableStock { get; set; }
}

public class CreateProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryID { get; set; }
    public string? SupplierID { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public decimal DefaultPrice { get; set; }
    public int InitialStock { get; set; }
}

public class UpdateProductDto
{
    public string? ProductName { get; set; }
    public string? CategoryID { get; set; }
    public string? Unit { get; set; }
    public decimal? WeightKG { get; set; }
    public decimal? VolumeLitre { get; set; }
    public int? ExpiryHours { get; set; }
    public string? StorageReq { get; set; }
    public decimal? DefaultPrice { get; set; }
    public string? Status { get; set; }
}
