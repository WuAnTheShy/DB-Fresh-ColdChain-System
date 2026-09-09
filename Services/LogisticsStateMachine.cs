using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>B 组履约编排使用的物流状态机；A 组正式实现应执行同等或更严格校验。</summary>
public static class LogisticsStateMachine
{
    public static bool CanTransition(string? current, string? target)
    {
        if (!LogisticsStatusCodes.IsSupported(current) ||
            !LogisticsStatusCodes.IsSupported(target))
            return false;

        var currentCode = LogisticsStatusCodes.Normalize(current);
        var targetCode = LogisticsStatusCodes.Normalize(target);
        if (currentCode == targetCode) return true;

        return (currentCode, targetCode) switch
        {
            (LogisticsStatusCodes.Shipped, LogisticsStatusCodes.InTransit) => true,
            (LogisticsStatusCodes.Shipped, LogisticsStatusCodes.Exception) => true,
            (LogisticsStatusCodes.Shipped, LogisticsStatusCodes.Returning) => true,
            (LogisticsStatusCodes.InTransit, LogisticsStatusCodes.OutForDelivery) => true,
            (LogisticsStatusCodes.InTransit, LogisticsStatusCodes.Delivered) => true,
            (LogisticsStatusCodes.InTransit, LogisticsStatusCodes.Exception) => true,
            (LogisticsStatusCodes.InTransit, LogisticsStatusCodes.Returning) => true,
            (LogisticsStatusCodes.OutForDelivery, LogisticsStatusCodes.Delivered) => true,
            (LogisticsStatusCodes.OutForDelivery, LogisticsStatusCodes.Exception) => true,
            (LogisticsStatusCodes.OutForDelivery, LogisticsStatusCodes.Returning) => true,
            (LogisticsStatusCodes.Exception, LogisticsStatusCodes.InTransit) => true,
            (LogisticsStatusCodes.Exception, LogisticsStatusCodes.Returning) => true,
            (LogisticsStatusCodes.Exception, LogisticsStatusCodes.Returned) => true,
            (LogisticsStatusCodes.Returning, LogisticsStatusCodes.Returned) => true,
            _ => false
        };
    }

    public static void EnsureTransition(string? current, string? target)
    {
        if (!CanTransition(current, target))
        {
            throw new OrderBusinessException(
                $"物流不能从“{LogisticsStatusCodes.GetName(current)}”变更为“{LogisticsStatusCodes.GetName(target)}”");
        }
    }
}
