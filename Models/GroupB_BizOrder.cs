using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// Biz_Orders - 订单主表
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

    // 收货地址按 1NF 拆成 4 个原子列存储（下单时的地址快照）。
    // 原复合列 ShippingAddress 已在拆分迁移中删除。
    public string? ReceiverProvince { get; set; }
    public string? ReceiverCity { get; set; }
    public string? ReceiverDistrict { get; set; }
    public string? ReceiverDetailAddress { get; set; }

    // 收货地址展示串（省 市 区 详址）。数据库侧已无此列，这里用只读计算属性拼出：
    // 无 setter，Dapper 映射与手写 INSERT 都不会写它；
    // 视图与跨组契约仍按「一段地址」使用，展示层无需改动。
    [NotMapped]
    public string ShippingAddress => ReceiverAddress.Format(
        ReceiverProvince,
        ReceiverCity,
        ReceiverDistrict,
        ReceiverDetailAddress);
    public decimal TotalAmount { get; set; }                // 商品总金额
    public decimal DiscountAmount { get; set; }             // 优惠券抵扣
    public decimal FreightAmount { get; set; }              // 运费
    public string? FreightQuoteSnapshot { get; set; }        // A 组运费报价 JSON 快照
    public decimal FinalAmount { get; set; }                // 实付金额
    public decimal? CommBaseAmount { get; set; }
    public decimal? CommBonusAmount { get; set; }
    public DateTime? CommSettlementDate { get; set; }
    public int PointsEarned { get; set; }                   // 该笔订单获得积分
    public int PointsUsed { get; set; }
    public decimal PointsDiscountAmount { get; set; }
    public string OrderStatus { get; set; } = OrderStatusCodes.PendingPayment;

    // 退款申请提交前的订单状态（仅当 OrderStatus 为 REFUND_REVIEWING 时有值）。
    // 退款申请被驳回或消费者取消申请时，订单状态回退到该值。
    public string? StatusBeforeRefund { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaymentExpiresAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
