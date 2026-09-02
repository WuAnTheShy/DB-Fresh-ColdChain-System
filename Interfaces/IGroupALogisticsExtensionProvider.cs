using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// A 组尚未提供的承运商、轨迹和温控扩展契约。
/// 当前由配置化内存兜底实现；A 组提供正式接口后替换 DI 注册即可。
/// </summary>
public interface IGroupALogisticsExtensionProvider
{
    Task<SupplierLogisticsSnapshot> RegisterShipmentAsync(
        LogisticsShipmentRegistration registration,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> GetSnapshotAsync(
        LogisticsTraceSeed seed,
        CancellationToken cancellationToken = default);

    Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
        LogisticsTrackingEventCommand command,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
