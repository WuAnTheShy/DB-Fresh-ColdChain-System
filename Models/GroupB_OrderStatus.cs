namespace FreshColdChain.Models;

// B 组订单状态。数据库和跨组接口使用 <see cref="OrderStatusCodes"/> 中的稳定字符串代码。
public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Shipped = 2,
    Completed = 3,
    Cancelled = 4,
    Refunding = 5,
    Refunded = 6,

    // 退款审核中：消费者已提交退款申请、平台财务尚未审核，审核驳回后回退到申请前状态。
    RefundReviewing = 7
}

// Biz_Orders.OrderStatus 的持久化代码，避免不同组对数字状态值产生歧义。
public static class OrderStatusCodes
{
    public const string PendingPayment = "PENDING_PAYMENT";
    public const string Paid = "PAID";
    public const string Shipped = "SHIPPED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Refunding = "REFUNDING";
    public const string Refunded = "REFUNDED";
    public const string RefundReviewing = "REFUND_REVIEWING";
    public const string Delivered = "DELIVERED";

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
            OrderStatus.RefundReviewing => RefundReviewing,
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
            RefundReviewing => OrderStatus.RefundReviewing,
            // 历史遗留：早期版本曾把物流签收态 DELIVERED 直接写入 Biz_Orders.OrderStatus。
            // 该值不在 CK_Order_Status 约束内，但存量数据中存在，按已完成交易兼容解析，
            // 避免订单列表/详情在序列化时因单个脏状态整体 500。
            Delivered => OrderStatus.Completed,
            _ => throw new ArgumentException($"未知订单状态代码: {code}", nameof(code))
        };
    }
}

// 订单状态的中文展示名称。
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
            OrderStatus.RefundReviewing => "退款审核中",
            _ => "未知状态"
        };
    }
}
