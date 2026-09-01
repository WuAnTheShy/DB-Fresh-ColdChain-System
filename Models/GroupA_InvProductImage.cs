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

    /// <summary>上传图片的供应商 ID；null = 平台通用图（所有供应商可见，供应商不可删除）</summary>
    [Column("SupplierID")]
    [MaxLength(36)]
    public string? SupplierID { get; set; }

    [Column("ImageUrl")]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// 图片二进制数据（BLOB）。供应商上传时写入数据库，
    /// 通过 /images/product/{ImageID} 接口读取，避免多机部署时本地文件丢失。
    /// </summary>
    [Column("ImageData")]
    public byte[]? ImageData { get; set; }

    /// <summary>图片 MIME 类型（如 image/webp）</summary>
    [Column("ImageType")]
    [MaxLength(20)]
    public string? ImageType { get; set; }

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>是否已有图片二进制数据（SQL 里由 DBMS_LOB.GETLENGTH 计算，仅查询填充）</summary>
    [NotMapped]
    public bool HasData { get; set; }
}
