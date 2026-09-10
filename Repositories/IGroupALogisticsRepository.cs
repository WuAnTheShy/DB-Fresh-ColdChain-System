using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 仅供 A 组持久化 Provider 使用，写操作必须参加调用方事务。
public interface IGroupALogisticsRepository
{
    Task<LogExpressDelivery?> GetDeliveryAsync(string orderId, string supplierId, bool forUpdate,
        IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogLogisticsEvent>> GetEventsAsync(string deliveryId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<LogLogisticsEvent?> GetEventAsync(string eventId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
    // 登记发货扩展信息：更新承运商/时效/备注并把 IsRegistered 置 1（扩展列已与基础发货单同表）。
    Task UpdateDetailAsync(LogExpressDelivery detail, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task InsertEventAsync(LogLogisticsEvent item, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateBaseStatusAsync(string deliveryId, string status, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
