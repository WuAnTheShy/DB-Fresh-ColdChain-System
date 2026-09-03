using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 供应商供货价（文档表名：Inv_SupplierPrices）
/// 一行 = 某供应商对某产品的报价，进价由供应商决定，入库时自动带出。
/// </summary>
[Table("Inv_SupplierPrices")]
public class InvSupplierPrice
{
    [Key]
    [Column("PriceID")]
    [MaxLength(36)]
    public string PriceID { get; set; } = Guid.NewGuid().ToString();

    [Column("SupplierID")]
    [MaxLength(36)]
    public string SupplierID { get; set; } = string.Empty;

    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("SupplyPrice")]
    public decimal SupplyPrice { get; set; }

    /// <summary>供应商声明的该产品保质期（小时），未声明时用产品典型保质期</summary>
    [Column("ShelfLifeHours")]
    public int? ShelfLifeHours { get; set; }

    /// <summary>该供应商对该商品的文字介绍；null 时兜底展示 Inv_Products.Description</summary>
    [Column("Description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    [NotMapped] public InvSupplier? Supplier { get; set; }
}
