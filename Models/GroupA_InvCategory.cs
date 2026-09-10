using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 商品分类（文档表名：Inv_Category）
[Table("Inv_Category")]
public class InvCategory
{
    [Key]
    [Column("CategoryID")]
    [MaxLength(36)]
    public string CategoryID { get; set; } = Guid.NewGuid().ToString();

    [Column("CategoryName")]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("ParentID")]
    [MaxLength(36)]
    public string? ParentID { get; set; }

    [NotMapped] public ICollection<InvProduct> Products { get; set; } = new List<InvProduct>();
}
