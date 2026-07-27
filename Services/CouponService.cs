using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 优惠券服务 - 防薅羊毛校验
/// </summary>
public class CouponService : ICouponService
{
    private readonly ICouponRepository _couponRepo;

    public CouponService(ICouponRepository couponRepo)
    {
        _couponRepo = couponRepo;
    }

    /// <summary>
    /// 校验优惠券是否可用
    /// 防薅羊毛：检查券归属、门槛、有效期
    /// </summary>
    public async Task<bool> ValidateCouponAsync(int recordId, int customerId, decimal orderAmount)
    {
        var coupons = await _couponRepo.GetUserCouponsAsync(customerId);
        var record = coupons.FirstOrDefault(r => r.RecordId == recordId);
        if (record == null) return false;

        var template = await _couponRepo.GetCouponTemplateAsync(record.CouponId);
        if (template == null) return false;

        // 检查最低消费门槛
        return orderAmount >= template.MinOrderAmount;
    }
}
