using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 库存
/// </summary>
[Table("INVENTORY")]
public class Inventory
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("PRODUCT_ID")]
    public int ProductId { get; set; }

    [Column("STOCK_QUANTITY")]
    public int StockQuantity { get; set; }

    [Column("LOCKED_QUANTITY")]
    public int LockedQuantity { get; set; }

    [Column("UPDATE_TIME")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }

    /// <summary>
    /// 可用库存 = 总库存 - 锁定库存
    /// </summary>
    [NotMapped]
    public int AvailableQuantity => StockQuantity - LockedQuantity;
}
