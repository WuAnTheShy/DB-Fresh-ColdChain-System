using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 供应商
/// </summary>
[Table("SUPPLIER")]
public class Supplier
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("CONTACT_PERSON")]
    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [Column("PHONE")]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Column("ADDRESS")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [Column("REMARK")]
    [MaxLength(1000)]
    public string? Remark { get; set; }

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    // 导航属性：一个供应商有多个产品
    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>产品数量（SQL 聚合查询填充，非数据库字段）</summary>
    [NotMapped]
    public int ProductCount { get; set; }
}
