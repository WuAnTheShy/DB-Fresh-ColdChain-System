namespace FreshColdChain.Models;

/// <summary>
/// Biz_Orders - 订单主表
/// </summary>
public class BizOrder
{
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;    // 订单号(展示用)
    public int CustomerId { get; set; }
    public int? PromoterId { get; set; }
    public int AddressId { get; set; }
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
    public int OrderStatus { get; set; }                    // 0=待支付 1=已支付 2=已发货 3=已完成 4=已取消 5=退款中 6=已退款
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
