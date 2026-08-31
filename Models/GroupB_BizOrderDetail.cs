namespace FreshColdChain.Models;

/// <summary>
/// Biz_OrderDetails - 订单明细
/// </summary>
public class BizOrderDetail
{
    public string OrderDetailId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }          // 下单时单价(快照)
    public decimal SubTotal { get; set; }            // 小计 = Quantity * UnitPrice
    public string? SupplierId { get; set; }          // 供应商ID（用于拆单）
    public string ReceiptStatus { get; set; } = "PENDING";
    public DateTime? ReceivedAt { get; set; }
}
