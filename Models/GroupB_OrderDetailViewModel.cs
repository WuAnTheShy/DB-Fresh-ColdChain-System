namespace FreshColdChain.Models;

/// <summary>
/// 订单详情页面模型。
/// </summary>
public sealed class OrderDetailViewModel
{
    public string OrderId { get; init; } = string.Empty;
    public BizOrder? Order { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public IReadOnlyList<BizOrderDetail> Details { get; init; } = [];
    public IReadOnlyList<OrderSupplierGroupViewModel> SupplierGroups { get; init; } = [];
    public bool CanShip { get; init; }
    public bool CanComplete { get; init; }
    public bool CanCancel { get; init; }

    public string StatusName => Order == null
        ? string.Empty
        : OrderStatusNames.GetName(OrderStatusCodes.Parse(Order.OrderStatus));
}

/// <summary>
/// 按供应商聚合的订单履约展示单元。
/// </summary>
public sealed class OrderSupplierGroupViewModel
{
    public string SupplierId { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public string FulfillmentStatus { get; init; } = "未同步";
    public string? TrackingNo { get; init; }
    public IReadOnlyList<BizOrderDetail> Items { get; init; } = [];
}

/// <summary>
/// Repository 查询订单和消费者名称的投影。
/// </summary>
public sealed class OrderDetailHeader
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? CheckoutBatchId { get; init; }
    public string? PromoterId { get; init; }
    public string AddressId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public int PointsEarned { get; init; }
    public int PointsUsed { get; init; }
    public decimal PointsDiscountAmount { get; init; }
    public string OrderStatus { get; init; } = OrderStatusCodes.PendingPayment;
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
            ShippingAddress = ShippingAddress,
            TotalAmount = TotalAmount,
            DiscountAmount = DiscountAmount,
            FreightAmount = FreightAmount,
            FinalAmount = FinalAmount,
            PointsEarned = PointsEarned,
            PointsUsed = PointsUsed,
            PointsDiscountAmount = PointsDiscountAmount,
            OrderStatus = OrderStatus,
            PaymentExpiresAt = PaymentExpiresAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
