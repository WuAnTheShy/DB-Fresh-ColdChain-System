namespace FreshColdChain.Models.DTOs;

/// <summary>
/// 商品上架搜索条目：一个“供应商 × 商品”的供货组合。
/// 供 C 组团长“商品上架”模块使用——搜索供应商得到其全部报价商品，
/// 搜索商品得到所有报价该商品的供应商。
/// </summary>
public class SupplierProductEntryDto
{
    public string SupplierID { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    /// <summary>商品计量单位（如：斤 / 盒 / 箱）</summary>
    public string? Unit { get; set; }

    /// <summary>该供应商对该商品的供货价</summary>
    public decimal SupplyPrice { get; set; }

    /// <summary>商品默认售价（参考）</summary>
    public decimal DefaultPrice { get; set; }

    /// <summary>保质期（小时），优先取供应商声明值，否则取商品典型值</summary>
    public int? ExpiryHours { get; set; }
}
