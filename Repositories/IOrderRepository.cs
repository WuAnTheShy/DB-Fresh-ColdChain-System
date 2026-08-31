using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IOrderRepository
{
    Task<string> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null);

    Task<BizOrder?> GetByIdAsync(string orderId, IDbTransaction? transaction = null);

    Task<BizOrder?> GetByIdForUpdateAsync(
        string orderId,
        IDbTransaction transaction);

    Task<List<BizOrder>> GetByCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction? transaction = null);

    Task<List<BizOrder>> GetByCheckoutBatchForUpdateAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction transaction);

    Task<List<BizOrder>> GetByCheckoutBatchForUpdateAsync(
        string checkoutBatchId,
        IDbTransaction transaction);

    Task<List<string>> GetExpiredPendingCheckoutBatchIdsAsync(
        DateTime now,
        IDbTransaction? transaction = null);

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
        string orderId,
        IDbTransaction? transaction = null);

    Task<List<BizOrderDetail>> GetDetailsAsync(
        string orderId,
        IDbTransaction? transaction = null);

    Task<bool> TryUpdateStatusAsync(
        string orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction);

    Task<bool> TryUpdateCommissionSettlementAsync(
        string orderId,
        decimal? commBaseAmount,
        decimal? commBonusAmount,
        DateTime? commSettlementDate,
        IDbTransaction transaction);

    Task InsertDetailsAsync(
        IEnumerable<BizOrderDetail> details,
        IDbTransaction? transaction = null);

    Task<decimal> GetCompletedSpentBeforeAsync(string customerId, DateTime cutoff, IDbTransaction? transaction = null);

    Task<bool> TryConfirmDetailReceiptAsync(
        string orderDetailId,
        string orderId,
        IDbTransaction transaction);

    Task<bool> HasUnreceivedDetailsExceptAsync(
        string orderId,
        string excludedOrderDetailId,
        IDbTransaction transaction);

    Task<bool> UpdatePointsEarnedAsync(
        string orderId,
        int pointsEarned,
        IDbTransaction transaction);
}
