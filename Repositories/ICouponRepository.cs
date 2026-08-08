using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICouponRepository
{
    Task<List<MktCouponRecord>> GetUserCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<MktCoupon?> GetCouponTemplateAsync(
        string couponId,
        IDbTransaction? transaction = null);

    Task<MktCoupon?> GetCouponTemplateForUpdateAsync(
        string couponId,
        IDbTransaction transaction);

    Task<List<ClaimableCouponItem>> GetClaimableCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<List<AvailableCouponItem>> GetAvailableCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<bool> HasCustomerClaimedCouponAsync(
        string customerId,
        string couponId,
        IDbTransaction? transaction = null);

    Task<string> CreateCouponRecordAsync(
        string recordId,
        string customerId,
        string couponId,
        IDbTransaction? transaction = null);

    Task<MktCouponUsage?> GetUsableCouponForUpdateAsync(
        string recordId,
        string customerId,
        decimal orderAmount,
        IDbTransaction transaction);

    Task<bool> TryUseCouponAsync(
        string recordId,
        string customerId,
        string orderId,
        IDbTransaction transaction);

    Task<int> RestoreCouponForCancelledOrderAsync(
        string orderId,
        string customerId,
        IDbTransaction transaction);

    Task<bool> DecrementCouponStockAsync(
        string couponId,
        IDbTransaction? transaction = null);
}
