using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

// 订单详情页面模型。
public sealed class OrderDetailViewModel
{
    private string? _displayStatusCode;
    private string? _displayStatusName;

    public string OrderId { get; init; } = string.Empty;
    public BizOrder? Order { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? PromoterName { get; init; }
    public IReadOnlyList<BizOrderDetail> Details { get; init; } = [];
    public IReadOnlyList<OrderCardProductItem> ProductItems { get; init; } = [];
    public IReadOnlyList<OrderSupplierGroupViewModel> SupplierGroups { get; init; } = [];
    public FreightCalculationResult? FreightQuote { get; init; }
    public bool CanShip { get; init; }
    public bool CanComplete { get; init; }
    public bool CanCancel { get; init; }

    public string DisplayStatusCode
    {
        get => _displayStatusCode ?? (Order == null
            ? string.Empty
            : OrderDisplayStatus.GetCode(
                Order.OrderStatus,
                SupplierGroups.Select(group => group.Logistics)));
        set => _displayStatusCode = value;
    }

    public string StatusName
    {
        get => _displayStatusName ?? (Order == null
            ? string.Empty
            : OrderDisplayStatus.GetName(
                Order.OrderStatus,
                SupplierGroups.Select(group => group.Logistics)));
        set => _displayStatusName = value;
    }
}

// 按供应商聚合的订单履约展示单元。
public sealed class OrderSupplierGroupViewModel
{
    public string SupplierId { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public string FulfillmentStatus { get; init; } = "未同步";
    public string? TrackingNo { get; init; }
    public SupplierLogisticsSnapshot Logistics { get; init; } = new();
    public IReadOnlyList<BizOrderDetail> Items { get; init; } = [];
}

// Repository 查询订单和消费者名称的投影。
public sealed class OrderDetailHeader
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? CheckoutBatchId { get; init; }
    public string? PromoterId { get; init; }
    public string? PromoterName { get; init; }
    public string AddressId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;

    // 收货地址的 4 个原子列（投影直接取 Biz_Orders 的对应列）
    public string? ReceiverProvince { get; init; }
    public string? ReceiverCity { get; init; }
    public string? ReceiverDistrict { get; init; }
    public string? ReceiverDetailAddress { get; init; }

    // 收货地址展示串（省 市 区 详址）。
    [NotMapped]
    public string ShippingAddress => ReceiverAddress.Format(
        ReceiverProvince,
        ReceiverCity,
        ReceiverDistrict,
        ReceiverDetailAddress);

    public decimal TotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public string? FreightQuoteSnapshot { get; init; }
    public decimal FinalAmount { get; init; }
    public int PointsEarned { get; init; }
    public int PointsUsed { get; init; }
    public decimal PointsDiscountAmount { get; init; }
    public string OrderStatus { get; init; } = OrderStatusCodes.PendingPayment;
    public string? StatusBeforeRefund { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PaymentExpiresAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public BizOrder ToOrder()
    {
        return new BizOrder
        {
            OrderId = OrderId,
            OrderNo = OrderNo,
            CustomerId = CustomerId,
            CheckoutBatchId = CheckoutBatchId,
            PromoterId = PromoterId,
            AddressId = AddressId,
            ReceiverName = ReceiverName,
            ReceiverPhone = ReceiverPhone,
            ReceiverProvince = ReceiverProvince,
            ReceiverCity = ReceiverCity,
            ReceiverDistrict = ReceiverDistrict,
            ReceiverDetailAddress = ReceiverDetailAddress,
            TotalAmount = TotalAmount,
            DiscountAmount = DiscountAmount,
            FreightAmount = FreightAmount,
            FreightQuoteSnapshot = FreightQuoteSnapshot,
            FinalAmount = FinalAmount,
            PointsEarned = PointsEarned,
            PointsUsed = PointsUsed,
            PointsDiscountAmount = PointsDiscountAmount,
            OrderStatus = OrderStatus,
            StatusBeforeRefund = StatusBeforeRefund,
            PaymentExpiresAt = PaymentExpiresAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
