using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 库存汇总（文档表名：Inv_StockSummary）
/// </summary>
[Table("Inv_StockSummary")]
public class InvStockSummary
{
    [Key]
    [Column("StockID")]
    [MaxLength(36)]
    public string StockID { get; set; } = Guid.NewGuid().ToString();

    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("TotalQty")]
    public int TotalQty { get; set; }

    [Column("LockedQty")]
    public int LockedQty { get; set; }

    [Column("AvailableQty")]
    public int AvailableQty { get; set; }

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    public InvProduct? Product { get; set; }
}
