using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 库存汇总（文档表名：Inv_StockSummary）
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

    // 可用量 = 总量 - 锁定（不小于 0）。
    // <b>数据库端是 Oracle 虚拟列</b>（GENERATED ALWAYS AS ... VIRTUAL），不落存储、
    // 由数据库按同一公式实时算出；这里用只读计算属性与之保持一致。
    // 因为无 setter，BaseRepository 的反射（按 CanWrite 过滤）会自动把它排除在
    // INSERT / UPDATE 之外，写入时不会再试图给虚拟列赋值。
    [Column("AvailableQty")]
    public int AvailableQty => Math.Max(TotalQty - LockedQty, 0);

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    [NotMapped] public InvProduct? Product { get; set; }
}
