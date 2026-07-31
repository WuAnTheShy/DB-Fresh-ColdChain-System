using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 商品分类（文档表名：Inv_Category）
/// </summary>
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

    public ICollection<InvProduct> Products { get; set; } = new List<InvProduct>();
}
