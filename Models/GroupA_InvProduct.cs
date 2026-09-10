using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 商品（文档表名：Inv_Products）
[Table("Inv_Products")]
public class InvProduct
{
    [Key]
    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = Guid.NewGuid().ToString();

    [Column("CategoryID")]
    [MaxLength(36)]
    public string? CategoryID { get; set; }

    [Column("ProductName")]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Column("Unit")]
    [MaxLength(20)]
    public string? Unit { get; set; }

    [Column("WeightKG")]
    public decimal? WeightKG { get; set; }

    [Column("VolumeLitre")]
    public decimal? VolumeLitre { get; set; }

    // 默认保质期（小时），货物可覆盖
    [Column("ExpiryHours")]
    public int? ExpiryHours { get; set; }

    // 默认温区（COLD/FROZEN/常温），货物可覆盖
    [Column("StorageReq")]
    [MaxLength(20)]
    public string? StorageReq { get; set; }

    // 物品通用文字介绍
    [Column("Description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    // 导航属性
    [NotMapped] public InvCategory? Category { get; set; }
    [NotMapped] public InvStockSummary? StockSummary { get; set; }
    [NotMapped] public ICollection<BizPriceRule> PriceRules { get; set; } = new List<BizPriceRule>();
}
