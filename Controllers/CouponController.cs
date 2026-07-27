using FreshColdChain.Interfaces;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

/// <summary>
/// 消费者优惠券中心。
/// </summary>
public sealed class CouponController : Controller
{
    private readonly ICouponService _couponService;
    private readonly ILogger<CouponController> _logger;

    public CouponController(
        ICouponService couponService,
        ILogger<CouponController> logger)
    {
        _couponService = couponService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int customerId = 1)
    {
        var model = await _couponService.GetCouponCenterAsync(customerId);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int customerId, int couponId)
    {
        try
        {
            await _couponService.ClaimCouponAsync(customerId, couponId);
            TempData["SuccessMessage"] = "优惠券领取成功";
        }
        catch (GroupBBusinessException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "消费者 {CustomerId} 领取优惠券 {CouponId} 失败",
                customerId,
                couponId);
            TempData["ErrorMessage"] = "系统暂时无法领取优惠券，请稍后重试";
        }

        return RedirectToAction(nameof(Index), new { customerId });
    }
}
