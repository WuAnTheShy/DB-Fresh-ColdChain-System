using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// 优惠券服务，负责可用券查询和防重复、防超发领取。
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
        string recordId,
        string customerId,
        decimal orderAmount)
    {
        if (!GroupBIds.IsValid(recordId) ||
            !GroupBIds.IsValid(customerId) ||
            orderAmount < 0)
            return false;

        var coupons = await _couponRepo.GetUserCouponsAsync(customerId);
        var record = coupons.FirstOrDefault(item => item.RecordId == recordId);
        if (record == null)
            return false;

        var template = await _couponRepo.GetCouponTemplateAsync(record.CouponId);
        return template != null && orderAmount >= template.MinOrderAmount;
    }

    public async Task<CouponCenterViewModel?> GetCouponCenterAsync(string customerId)
    {
        if (!GroupBIds.IsValid(customerId))
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

    public async Task ClaimCouponAsync(string customerId, string couponId)
    {
        if (!GroupBIds.IsValid(customerId))
            throw new GroupBBusinessException("消费者ID格式不正确");
        if (!GroupBIds.IsValid(couponId))
            throw new GroupBBusinessException("优惠券ID格式不正确");

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
                GroupBIds.NewId(),
                customerId,
                couponId,
                transaction);
        });
    }
}
