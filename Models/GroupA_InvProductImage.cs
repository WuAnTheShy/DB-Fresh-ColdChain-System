using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

/// <summary>
/// 商品图片（表名：Inv_ProductImages）
/// 一个商品对应多张图片，对外展示取 SortOrder 升序前 3 张。
/// </summary>
[Table("Inv_ProductImages")]
public class InvProductImage
{
    [Key]
    [Column("ImageID")]
    [MaxLength(36)]
    public string ImageID { get; set; } = Guid.NewGuid().ToString();

    [Column("ProductID")]
    [MaxLength(36)]
    public string ProductID { get; set; } = string.Empty;

    [Column("ImageUrl")]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
