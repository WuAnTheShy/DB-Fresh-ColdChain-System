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

    /// <summary>商品文字介绍（供应商维护，团长可参考/复制/改写）</summary>
    public string? Description { get; set; }

    /// <summary>商品图片（按展示顺序，最多 3 张）</summary>
    public List<string> Images { get; set; } = new();
}

/// <summary>
/// 团长已入团商品详情：在 SupplierProductEntryDto 基础上附带团长定价，
/// 供“商品上架”页已上架区、团长工作台速览使用。
/// </summary>
public class PromoterProductEntryDetailDto
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

    /// <summary>团长定价（null 表示未定价，展示时默认取推荐价）</summary>
    public decimal? PromoterPrice { get; set; }

    /// <summary>商品文字介绍（供应商维护，团长可参考/复制/改写）</summary>
    public string? Description { get; set; }

    /// <summary>团长带货介绍文字（入团时默认复制供应商文字，团长可修改/重写）</summary>
    public string? PromoterDesc { get; set; }

    /// <summary>商品图片（按展示顺序，最多 3 张）</summary>
    public List<string> Images { get; set; } = new();
}
