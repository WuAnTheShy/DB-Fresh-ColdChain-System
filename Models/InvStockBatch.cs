using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 库存批次 — 实现 FEFO 先进先出（文档表名：Inv_StockBatches）
/// </summary>
[Table("Inv_StockBatches")]
public class InvStockBatch
{
    [Key]
    [Column("BatchID")]
    [MaxLength(36)]
    public string BatchID { get; set; } = Guid.NewGuid().ToString();

    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("SupplierID")]
    [MaxLength(36)]
    public string? SupplierID { get; set; }

    [Column("BatchNo")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

    [Column("ProductionDate")]
    public DateTime? ProductionDate { get; set; }

    [Column("ExpiryDate")]
    public DateTime? ExpiryDate { get; set; }

    [Column("InPrice")]
    public decimal InPrice { get; set; }

    [Column("InitialQty")]
    public int InitialQty { get; set; }

    [Column("CurrentQty")]
    public int CurrentQty { get; set; }

    [Column("Status")]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    public InvProduct? Product { get; set; }
}
