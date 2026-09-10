namespace FreshColdChain.Models;

// 消费者端订单展示状态。订单在确认收货前仍保持 SHIPPED，
// 展示层根据各供应商包裹的实时状态提供更准确的描述。
public static class OrderDisplayStatus
{
    public static string GetCode(
        string orderStatus,
        IEnumerable<SupplierLogisticsSnapshot>? logisticsSnapshots)
    {
        var status = OrderStatusCodes.Parse(orderStatus);
        if (status != OrderStatus.Shipped)
            return OrderStatusCodes.ToCode(status);

        var packageStatuses = GetPackageStatuses(logisticsSnapshots);
        return packageStatuses.Count == 0
            ? OrderStatusCodes.Shipped
            : ResolveLogisticsCode(packageStatuses);
    }

    public static string GetName(
        string orderStatus,
        IEnumerable<SupplierLogisticsSnapshot>? logisticsSnapshots)
    {
        var status = OrderStatusCodes.Parse(orderStatus);
        if (status != OrderStatus.Shipped)
            return OrderStatusNames.GetName(status);

        var packageStatuses = GetPackageStatuses(logisticsSnapshots);
        if (packageStatuses.Count == 0)
            return OrderStatusNames.GetName(OrderStatus.Shipped);

        var displayCode = ResolveLogisticsCode(packageStatuses);
        if (packageStatuses.Distinct(StringComparer.Ordinal).Count() == 1)
            return LogisticsStatusCodes.GetName(displayCode);

        return displayCode switch
        {
            LogisticsStatusCodes.Exception => "部分包裹物流异常",
            LogisticsStatusCodes.Returning => "部分包裹退回中",
            LogisticsStatusCodes.Returned => "部分包裹已退回",
            LogisticsStatusCodes.Delivered => "部分包裹已签收",
            LogisticsStatusCodes.OutForDelivery => "部分包裹派送中",
            LogisticsStatusCodes.InTransit => "部分包裹运输中",
            LogisticsStatusCodes.Shipped => "部分包裹已发货",
            LogisticsStatusCodes.Packing => "部分包裹备货中",
            _ => "部分包裹待发货"
        };
    }

    private static List<string> GetPackageStatuses(
        IEnumerable<SupplierLogisticsSnapshot>? logisticsSnapshots) =>
        logisticsSnapshots?
            .Select(snapshot => LogisticsStatusCodes.Normalize(snapshot.StatusCode))
            .ToList() ?? [];

    private static string ResolveLogisticsCode(IReadOnlyCollection<string> packageStatuses)
    {
        if (packageStatuses.Contains(LogisticsStatusCodes.Exception))
            return LogisticsStatusCodes.Exception;
        if (packageStatuses.Contains(LogisticsStatusCodes.Returning))
            return LogisticsStatusCodes.Returning;
        if (packageStatuses.Contains(LogisticsStatusCodes.Returned))
            return LogisticsStatusCodes.Returned;
        if (packageStatuses.Contains(LogisticsStatusCodes.Delivered))
            return LogisticsStatusCodes.Delivered;
        if (packageStatuses.Contains(LogisticsStatusCodes.OutForDelivery))
            return LogisticsStatusCodes.OutForDelivery;
        if (packageStatuses.Contains(LogisticsStatusCodes.InTransit))
            return LogisticsStatusCodes.InTransit;
        if (packageStatuses.Contains(LogisticsStatusCodes.Shipped))
            return LogisticsStatusCodes.Shipped;
        if (packageStatuses.Contains(LogisticsStatusCodes.Packing))
            return LogisticsStatusCodes.Packing;
        return LogisticsStatusCodes.Pending;
    }
}
