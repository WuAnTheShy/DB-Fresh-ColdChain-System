namespace FreshColdChain.Models;

/// <summary>
/// Biz_Orders - 订单主表
/// </summary>
public class BizOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;    // 订单号(展示用)
    public string CustomerId { get; set; } = string.Empty;
    public string? CheckoutBatchId { get; set; }
    public string? PromoterId { get; set; }
    public string AddressId { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }                // 商品总金额
    public decimal DiscountAmount { get; set; }             // 优惠券抵扣
    public decimal FreightAmount { get; set; }              // 运费
    public decimal FinalAmount { get; set; }                // 实付金额
    public decimal? CommBaseAmount { get; set; }
    public decimal? CommBonusAmount { get; set; }
    public DateTime? CommSettlementDate { get; set; }
    public int PointsEarned { get; set; }                   // 该笔订单获得积分
    public string OrderStatus { get; set; } = OrderStatusCodes.PendingPayment;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaymentExpiresAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
