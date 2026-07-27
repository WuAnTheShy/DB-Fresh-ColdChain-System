using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 优惠券服务，负责可用券查询和防重复、防超发领取。
/// </summary>
public sealed class CouponService : ICouponService
{
    private readonly ICouponRepository _couponRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IOrderTransactionManager _transactionManager;

    public CouponService(
        ICouponRepository couponRepo,
        ICustomerRepository customerRepo,
        IOrderTransactionManager transactionManager)
    {
        _couponRepo = couponRepo;
        _customerRepo = customerRepo;
        _transactionManager = transactionManager;
    }

    public async Task<bool> ValidateCouponAsync(
        int recordId,
        int customerId,
        decimal orderAmount)
    {
        if (recordId <= 0 || customerId <= 0 || orderAmount < 0)
            return false;

        var coupons = await _couponRepo.GetUserCouponsAsync(customerId);
        var record = coupons.FirstOrDefault(item => item.RecordId == recordId);
        if (record == null)
            return false;

        var template = await _couponRepo.GetCouponTemplateAsync(record.CouponId);
        return template != null && orderAmount >= template.MinOrderAmount;
    }

    public async Task<CouponCenterViewModel?> GetCouponCenterAsync(int customerId)
    {
        if (customerId <= 0)
            return null;

        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null)
            return null;

        var claimableTask = _couponRepo.GetClaimableCouponsAsync(customerId);
        var availableTask = _couponRepo.GetAvailableCouponsAsync(customerId);
        await Task.WhenAll(claimableTask, availableTask);

        return new CouponCenterViewModel
        {
            CustomerId = customerId,
            CustomerName = customer.CustomerName,
            ClaimableCoupons = await claimableTask,
            AvailableCoupons = await availableTask
        };
    }

    public async Task ClaimCouponAsync(int customerId, int couponId)
    {
        if (customerId <= 0)
            throw new GroupBBusinessException("消费者ID必须大于0");
        if (couponId <= 0)
            throw new GroupBBusinessException("优惠券ID必须大于0");

        await _transactionManager.ExecuteAsync(async transaction =>
        {
            _ = await _customerRepo.GetByIdForUpdateAsync(customerId, transaction)
                ?? throw new GroupBBusinessException("消费者不存在");
            var coupon = await _couponRepo.GetCouponTemplateForUpdateAsync(
                    couponId,
                    transaction)
                ?? throw new GroupBBusinessException("优惠券不存在");

            var now = DateTime.Now;
            if (coupon.Status != 1 || coupon.StartTime > now || coupon.EndTime < now)
                throw new GroupBBusinessException("优惠券当前不可领取");
            if (coupon.RemainingQuantity <= 0)
                throw new GroupBBusinessException("优惠券已领完");
            if (await _couponRepo.HasCustomerClaimedCouponAsync(
                customerId,
                couponId,
                transaction))
            {
                throw new GroupBBusinessException("每位消费者限领一张，请勿重复领取");
            }

            if (!await _couponRepo.DecrementCouponStockAsync(couponId, transaction))
                throw new GroupBBusinessException("优惠券已领完或活动已结束");

            _ = await _couponRepo.CreateCouponRecordAsync(
                customerId,
                couponId,
                transaction);
        });
    }
}
