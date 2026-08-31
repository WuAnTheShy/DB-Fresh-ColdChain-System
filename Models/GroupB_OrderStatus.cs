namespace FreshColdChain.Models;

/// <summary>
/// B 组订单状态。数据库和跨组接口使用 <see cref="OrderStatusCodes"/> 中的稳定字符串代码。
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
/// Biz_Orders.OrderStatus 的持久化代码，避免不同组对数字状态值产生歧义。
/// </summary>
public static class OrderStatusCodes
{
    public const string PendingPayment = "PENDING_PAYMENT";
    public const string Paid = "PAID";
    public const string Shipped = "SHIPPED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Refunding = "REFUNDING";
    public const string Refunded = "REFUNDED";

    public static string ToCode(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.PendingPayment => PendingPayment,
            OrderStatus.Paid => Paid,
            OrderStatus.Shipped => Shipped,
            OrderStatus.Completed => Completed,
            OrderStatus.Cancelled => Cancelled,
            OrderStatus.Refunding => Refunding,
            OrderStatus.Refunded => Refunded,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "订单状态无效")
        };
    }

    public static OrderStatus Parse(string code)
    {
        return code switch
        {
            PendingPayment => OrderStatus.PendingPayment,
            Paid => OrderStatus.Paid,
            Shipped => OrderStatus.Shipped,
            Completed => OrderStatus.Completed,
            Cancelled => OrderStatus.Cancelled,
            Refunding => OrderStatus.Refunding,
            Refunded => OrderStatus.Refunded,
            _ => throw new ArgumentException($"未知订单状态代码: {code}", nameof(code))
        };
    }
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
