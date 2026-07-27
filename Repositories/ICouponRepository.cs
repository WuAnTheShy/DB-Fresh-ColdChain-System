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

    Task<bool> DecrementCouponStockAsync(
        int couponId,
        IDbTransaction? transaction = null);
}
