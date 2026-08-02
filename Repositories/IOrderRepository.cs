using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null);

    Task<BizOrder?> GetByIdAsync(int orderId, IDbTransaction? transaction = null);

    Task<BizOrder?> GetByIdForUpdateAsync(
        int orderId,
        IDbTransaction transaction);

    Task<List<BizOrder>> GetOrdersForCommissionExpiryAsync(
        DateTime threshold,
        IDbTransaction? transaction = null);

    Task<int> CountOrdersAsync(
        OrderQueryRequest request,
        IDbTransaction? transaction = null);

    Task<List<OrderListItem>> GetOrdersAsync(
        OrderQueryRequest request,
        int offset,
        IDbTransaction? transaction = null);

    Task<OrderDetailHeader?> GetDetailHeaderAsync(
        int orderId,
        IDbTransaction? transaction = null);

    Task<List<BizOrderDetail>> GetDetailsAsync(
        int orderId,
        IDbTransaction? transaction = null);

    Task<bool> TryUpdateStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction);

    Task<bool> TryUpdateCommissionSettlementAsync(
        int orderId,
        decimal? commBaseAmount,
        decimal? commBonusAmount,
        DateTime? commSettlementDate,
        IDbTransaction transaction);

    Task InsertDetailsAsync(
        IEnumerable<BizOrderDetail> details,
        IDbTransaction? transaction = null);
}
