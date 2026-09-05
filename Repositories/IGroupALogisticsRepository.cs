using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>仅供 A 组持久化 Provider 使用，写操作必须参加调用方事务。</summary>
public interface IGroupALogisticsRepository
{
    Task<LogExpressDelivery?> GetDeliveryAsync(string orderId, string supplierId, bool forUpdate,
        IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<LogLogisticsDetail?> GetDetailAsync(string deliveryId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogLogisticsEvent>> GetEventsAsync(string deliveryId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<LogLogisticsEvent?> GetEventAsync(string eventId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
    Task InsertDetailAsync(LogLogisticsDetail detail, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task InsertEventAsync(LogLogisticsEvent item, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateBaseStatusAsync(string deliveryId, string status, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
