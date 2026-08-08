namespace FreshColdChain.Models;

/// <summary>
/// Biz_OrderDetails - 订单明细
/// </summary>
public class BizOrderDetail
{
    public int OrderDetailId { get; set; }
    public int OrderId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }          // 下单时单价(快照)
    public decimal SubTotal { get; set; }            // 小计 = Quantity * UnitPrice
    public string? SupplierId { get; set; }          // 供应商ID（用于拆单）
}
