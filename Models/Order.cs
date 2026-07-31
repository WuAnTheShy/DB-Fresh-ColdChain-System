using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 订单
/// </summary>
[Table("ORDER")]
public class Order
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("ORDER_NO")]
    [MaxLength(50)]
    public string OrderNo { get; set; } = string.Empty;

    [Column("GROUP_LEADER_ID")]
    public int GroupLeaderId { get; set; }

    [Column("TOTAL_AMOUNT")]
    public decimal TotalAmount { get; set; }

    [Column("STATUS")]
    public int Status { get; set; } = 1;
    // 1=待确认, 2=已确认, 3=配送中, 4=已完成, 5=已取消

    [Column("REMARK")]
    [MaxLength(1000)]
    public string? Remark { get; set; }

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Column("UPDATE_TIME")]
    public DateTime UpdateTime { get; set; } = DateTime.Now;

    // 导航属性（Dapper multi-mapping 填充）
    public GroupLeader? GroupLeader { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
