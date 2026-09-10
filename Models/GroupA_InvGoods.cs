using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 货物 — 供应商对某物品的供货条目（文档表名：Inv_Goods）。
// 主码 = (ProductID, SupplierID)：同一物品可由多个供应商供货，各自是独立货物，
// 拥有独立的售价、上下架状态、温区等。售价( SalePrice )单独落库；
// supplyprice（最终供货价 = 团长进价）由价格规则实时计算，不落库。
[Table("Inv_Goods")]
public class InvGoods
{
    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("SupplierID")]
    [MaxLength(36)]
    public string SupplierID { get; set; } = string.Empty;

    // 售价（供应商设定，单独记录）
    [Column("SalePrice")]
    public decimal SalePrice { get; set; }

    // 上下架：ACTIVE=上架, INACTIVE=下架
    [Column("Status")]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    // 温区（CHILLED / FROZEN / AMBIENT）。<b>不落库</b>：温区是「物品」自己的属性，
    // 货物侧不再单独覆盖，该值由 GoodsRepository 联表 Inv_Products 带出，仅供展示。
    [NotMapped]
    public string? StorageReq { get; set; }

    // 保质期（小时），供应商可覆盖物品默认值
    [Column("ShelfLifeHours")]
    public int? ShelfLifeHours { get; set; }

    // 供应商图文介绍
    [Column("Description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    [NotMapped] public InvProduct? Product { get; set; }
    [NotMapped] public InvSupplier? Supplier { get; set; }
}
