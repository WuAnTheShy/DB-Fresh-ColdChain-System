namespace FreshColdChain.Models.DTOs;

/// <summary>货物展示 DTO（供应商门户 / 管理员界面）</summary>
public class GoodsDto
{
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? StorageReq { get; set; }
    public int? ShelfLifeHours { get; set; }
    public string? Description { get; set; }
}

/// <summary>供应商建立货物：对现有物品建立自己的供货条目（不设数量上限，数量走进货）</summary>
public class CreateGoodsDto
{
    public string ProductID { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string? StorageReq { get; set; }
    public int? ShelfLifeHours { get; set; }
    public string? Description { get; set; }
}

/// <summary>供应商维护货物：改售价 / 温区 / 保质期 / 描述 / 上下架</summary>
public class UpdateGoodsDto
{
    public decimal? SalePrice { get; set; }
    public string? StorageReq { get; set; }
    public int? ShelfLifeHours { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}
