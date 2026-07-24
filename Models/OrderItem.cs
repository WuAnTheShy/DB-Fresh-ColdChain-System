using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshGroupSystem.Models;

/// <summary>
/// 订单明细
/// </summary>
[Table("ORDER_ITEM")]
public class OrderItem
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("ORDER_ID")]
    public int OrderId { get; set; }

    [Column("PRODUCT_ID")]
    public int ProductId { get; set; }

    [Column("QUANTITY")]
    public int Quantity { get; set; }

    [Column("UNIT_PRICE")]
    public decimal UnitPrice { get; set; }

    [Column("SUBTOTAL")]
    public decimal Subtotal { get; set; }

    // 导航属性
    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }
}
