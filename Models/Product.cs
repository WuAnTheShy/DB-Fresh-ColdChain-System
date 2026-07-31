using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 产品
/// </summary>
[Table("PRODUCT")]
public class Product
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("CATEGORY")]
    [MaxLength(100)]
    public string? Category { get; set; }

    [Column("UNIT")]
    [MaxLength(20)]
    public string? Unit { get; set; }

    [Column("PRICE")]
    public decimal Price { get; set; }

    [Column("IMAGE_URL")]
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Column("STATUS")]
    public int Status { get; set; } = 1;  // 1=上架, 0=下架

    [Column("SUPPLIER_ID")]
    public int SupplierId { get; set; }

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    // 导航属性（Dapper multi-mapping 填充）
    public Supplier? Supplier { get; set; }

    public Inventory? Inventory { get; set; }
}
