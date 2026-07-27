using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICouponRepository
{
    Task<List<MktCouponRecord>> GetUserCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<MktCoupon?> GetCouponTemplateAsync(
        int couponId,
        IDbTransaction? transaction = null);

    Task<MktCoupon?> GetCouponTemplateForUpdateAsync(
        int couponId,
        IDbTransaction transaction);

    Task<List<ClaimableCouponItem>> GetClaimableCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<List<AvailableCouponItem>> GetAvailableCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<bool> HasCustomerClaimedCouponAsync(
        int customerId,
        int couponId,
        IDbTransaction? transaction = null);

    Task<int> CreateCouponRecordAsync(
        int customerId,
        int couponId,
        IDbTransaction transaction);

    Task<MktCouponUsage?> GetUsableCouponForUpdateAsync(
        int recordId,
        int customerId,
        decimal orderAmount,
        IDbTransaction transaction);

    Task<bool> TryUseCouponAsync(
        int recordId,
        int customerId,
        int orderId,
        IDbTransaction transaction);

    Task<int> RestoreCouponForCancelledOrderAsync(
        int orderId,
        int customerId,
        IDbTransaction transaction);

    Task<bool> DecrementCouponStockAsync(
        int couponId,
        IDbTransaction? transaction = null);
}
