using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 商品入团表（表名：CRM_PRODUCT_ENTRIES）
/// 一行 = 某团长将（某供应商供应的某商品）加入其入团/可售商品集合。
/// 团长与商品为多对多关系，同一商品可由不同供应商供货，故三元组为主键。
/// </summary>
[Table("CRM_PRODUCT_ENTRIES")]
public class GroupC_CrmProductEntry
{
    [Column("PROMOTERID")]
    [MaxLength(36)]
    public string PromoterId { get; set; } = string.Empty;

    [Column("PRODUCTID")]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    [Column("SUPPLIERID")]
    [MaxLength(36)]
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Active=已入团 Inactive=已移除</summary>
    [Column("STATUS")]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// 团长定价（团长对（商品，供应商）组合的自定售价）。
    /// 未填写时默认为商品推荐价（DefaultPrice）。
    /// 定价规则：|团长价 - 推荐价| &lt; |推荐价 - 报价| / 2。
    /// </summary>
    [Column("PROMOTERPRICE")]
    public decimal? PromoterPrice { get; set; }

    [Column("CREATETIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Column("UPDATETIME")]
    public DateTime? UpdateTime { get; set; }
}
