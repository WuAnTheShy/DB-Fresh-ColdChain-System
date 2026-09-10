using FreshColdChain.Models;

namespace FreshColdChain.Services;

// B 组订单状态机，禁止 Controller 或 Repository 绕过业务规则直接改状态。
public static class OrderStateMachine
{
    public static bool CanTransition(OrderStatus current, OrderStatus target)
    {
        return (current, target) switch
        {
            (OrderStatus.Paid, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Completed) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            (OrderStatus.PendingPayment, OrderStatus.Cancelled) => true,

            // 退款审核：提交退款申请后订单进入“退款审核中”（原状态记录在 Biz_Orders.StatusBeforeRefund），
            // 审核驳回/消费者取消时回退到申请前状态，审核通过则由资金操作推进为“退款中/已退款”。
            // 注意：进入审核中后不允许再发货、完成或取消订单。
            (OrderStatus.Paid, OrderStatus.RefundReviewing) => true,
            (OrderStatus.Shipped, OrderStatus.RefundReviewing) => true,
            (OrderStatus.Completed, OrderStatus.RefundReviewing) => true,
            (OrderStatus.Refunding, OrderStatus.RefundReviewing) => true,
            (OrderStatus.RefundReviewing, OrderStatus.Paid) => true,
            (OrderStatus.RefundReviewing, OrderStatus.Shipped) => true,
            (OrderStatus.RefundReviewing, OrderStatus.Completed) => true,
            (OrderStatus.RefundReviewing, OrderStatus.Refunding) => true,
            (OrderStatus.RefundReviewing, OrderStatus.Refunded) => true,
            (OrderStatus.Refunding, OrderStatus.Refunded) => true,
            _ => false
        };
    }

    public static void EnsureTransition(OrderStatus current, OrderStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new OrderBusinessException(
                $"订单不能从“{OrderStatusNames.GetName(current)}”变更为“{OrderStatusNames.GetName(target)}”");
        }
    }
}
