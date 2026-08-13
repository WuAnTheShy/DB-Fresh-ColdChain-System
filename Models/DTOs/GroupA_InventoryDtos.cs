namespace FreshColdChain.Models.DTOs;

public class InventoryDto
{
    public string StockID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int TotalQty { get; set; }
    public int LockedQty { get; set; }
    public int AvailableQty { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class UpdateInventoryDto
{
    public string ProductID { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class StockBatchDto
{
    public string BatchID { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal InPrice { get; set; }
    public int InitialQty { get; set; }
    public int CurrentQty { get; set; }
    public string Status { get; set; } = "ACTIVE";
}

public class CreateStockBatchDto
{
    public string ProductID { get; set; } = string.Empty;
    public string? SupplierID { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int InitialQty { get; set; }
}
