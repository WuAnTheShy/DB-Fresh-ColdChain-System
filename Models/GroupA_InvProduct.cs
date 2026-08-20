using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 商品（文档表名：Inv_Products）
/// </summary>
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

    [Column("SupplierID")]
    [MaxLength(36)]
    public string? SupplierID { get; set; }

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

    [Column("ExpiryHours")]
    public int? ExpiryHours { get; set; }

    [Column("StorageReq")]
    [MaxLength(20)]
    public string? StorageReq { get; set; }

    [Column("DefaultPrice")]
    public decimal DefaultPrice { get; set; }

    [Column("Status")]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE=上架, INACTIVE=下架

    // 导航属性
    [NotMapped] public InvCategory? Category { get; set; }
    [NotMapped] public InvSupplier? Supplier { get; set; }
    [NotMapped] public InvStockSummary? StockSummary { get; set; }
    [NotMapped] public ICollection<BizPriceRule> PriceRules { get; set; } = new List<BizPriceRule>();
}
