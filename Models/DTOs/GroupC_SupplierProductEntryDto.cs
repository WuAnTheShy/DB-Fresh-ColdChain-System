namespace FreshColdChain.Models.DTOs;

/// <summary>
/// 商品上架搜索条目：一个“供应商 × 商品”的供货组合。
/// 供 C 组团长“商品上架”模块使用——以供应商的货物（Inv_Goods）为准：
/// 搜索供应商得到其全部上架货物商品，搜索商品得到所有建立货物供货的供应商。
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

    /// <summary>该供应商对该商品的上架状态（Inv_Goods.Status：ACTIVE=上架，其它=下架）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>该供应商是否仍上架该商品（下架后为 false，团长端应显示“已下架”且不可入团）</summary>
    public bool IsProductActive => string.Equals(ProductStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>商品加入团长带货列表的时间</summary>
    public DateTime CreateTime { get; set; }

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

    /// <summary>
    /// 团长带货介绍存储值：空=无介绍；以 /uploads/promoter-desc/*.json 开头且以 .json 结尾=图文内容文件相对路径；
    /// 其它非空串=历史纯文字（兼容读取，团长保存图文介绍后转为相对路径）。
    /// </summary>
    public string? PromoterDesc { get; set; }

    /// <summary>商品图片（按展示顺序，最多 3 张）</summary>
    public List<string> Images { get; set; } = new();

    /// <summary>该供应商对该商品的上架状态（Inv_Goods.Status：ACTIVE=上架，其它=下架）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>该供应商是否仍上架该商品（下架后为 false，团长端应显示“已下架”）</summary>
    public bool IsProductActive => string.Equals(ProductStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
}
