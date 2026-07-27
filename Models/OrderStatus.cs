namespace FreshColdChain.Models;

/// <summary>
/// B 组订单状态，数值与 Biz_Orders.OrderStatus 保持一致。
/// </summary>
public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Shipped = 2,
    Completed = 3,
    Cancelled = 4,
    Refunding = 5,
    Refunded = 6
}

/// <summary>
/// 订单状态的中文展示名称。
/// </summary>
public static class OrderStatusNames
{
    public static string GetName(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.PendingPayment => "待支付",
            OrderStatus.Paid => "已支付",
            OrderStatus.Shipped => "已发货",
            OrderStatus.Completed => "已完成",
            OrderStatus.Cancelled => "已取消",
            OrderStatus.Refunding => "退款中",
            OrderStatus.Refunded => "已退款",
            _ => "未知状态"
        };
    }
}
