namespace FreshGroupSystem.Models.DTOs;

/// <summary>
/// 库存更新
/// </summary>
public class UpdateInventoryDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }  // 正数=入库, 负数=出库
}

public class InventoryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int LockedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime UpdateTime { get; set; }
}
