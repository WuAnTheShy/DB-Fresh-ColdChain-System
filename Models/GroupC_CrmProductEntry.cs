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

    /// <summary>
    /// 动态报价快照：入团/上架时刻按“供应商动态定价（规则引擎）”计算的最终报价（团长进价），
    /// 入库后不再随货物售价与价格规则实时漂移，供团长端/消费者端离线展示。
    /// </summary>
    [Column("SUPPLYPRICE")]
    public decimal? SupplyPrice { get; set; }

    /// <summary>
    /// 推荐价快照：入团时刻动态报价 × 1.2（1.2 倍率不变），
    /// 团长未定价时即作为默认售价展示。
    /// </summary>
    [Column("DEFAULTPRICE")]
    public decimal? DefaultPrice { get; set; }

    /// <summary>
    /// 团长带货介绍存储值（图文介绍改造后）：空=无介绍；
    /// /uploads/promoter-desc/*.json=图文内容文件相对路径；
    /// 其它非空串=历史纯文字介绍（兼容读取，团长保存图文介绍后转为相对路径）。
    /// </summary>
    [Column("PROMOTERDESC")]
    [MaxLength(2000)]
    public string? PromoterDesc { get; set; }

    [Column("CREATETIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Column("UPDATETIME")]
    public DateTime? UpdateTime { get; set; }
}
