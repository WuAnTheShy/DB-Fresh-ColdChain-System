using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// A 组承运商、轨迹和温控扩展契约，默认使用 Oracle 持久化。
// 写入参与调用方事务；内存兜底仅允许在开发环境显式启用。
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
