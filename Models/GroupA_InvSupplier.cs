using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 供应商（文档表名：Inv_Suppliers）
[Table("Inv_Suppliers")]
public class InvSupplier
{
    [Key]
    [Column("SupplierID")]
    [MaxLength(36)]
    public string SupplierID { get; set; } = Guid.NewGuid().ToString();

    [Column("SupplierName")]
    [MaxLength(100)]
    public string SupplierName { get; set; } = string.Empty;

    [Column("LicenseNo")]
    [MaxLength(100)]
    public string? LicenseNo { get; set; }

    [Column("ExpiryDate")]
    public DateTime? ExpiryDate { get; set; }

    [Column("CreditLevel")]
    public int CreditLevel { get; set; }

    [Column("ContactPhone")]
    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [Column("LoginAccount")]
    [MaxLength(50)]
    public string? LoginAccount { get; set; }

    [Column("LoginPassword")]
    [MaxLength(255)]
    public string? LoginPassword { get; set; }

    // 状态: Pending(待审核) / Active(正常) / Disabled(已禁用) / Rejected(入驻被驳回)
    [Column("STATUS")]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    // 导航属性
    [NotMapped] public ICollection<InvProduct> Products { get; set; } = new List<InvProduct>();

    // 产品数量（SQL 聚合查询填充）
    [NotMapped]
    public int ProductCount { get; set; }
}
