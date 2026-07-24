using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 团长（社区团购负责人）
/// </summary>
[Table("GROUP_LEADER")]
public class GroupLeader
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("NAME")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("PHONE")]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Column("COMMUNITY_NAME")]
    [MaxLength(200)]
    public string? CommunityName { get; set; }

    [Column("ADDRESS")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [Column("STATUS")]
    public int Status { get; set; } = 1;  // 1=正常, 0=停用

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    // 导航属性：一个团长有多个订单
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
