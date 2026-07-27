using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>
/// B 组订单状态机，禁止 Controller 或 Repository 绕过业务规则直接改状态。
/// </summary>
public static class OrderStateMachine
{
    public static bool CanTransition(OrderStatus current, OrderStatus target)
    {
        return (current, target) switch
        {
            (OrderStatus.Paid, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Completed) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
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
