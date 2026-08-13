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

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}
