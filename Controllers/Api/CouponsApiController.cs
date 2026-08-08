using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/customers/{customerId}/coupons")]
public sealed class CouponsApiController(
    ICouponService couponService) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetCouponCenter(string customerId)
    {
        var result = await couponService.GetCouponCenterAsync(customerId);
        return result == null
            ? ApiNotFound("消费者不存在")
            : Ok(result);
    }

    [HttpPost("{couponId}/claim")]
    public async Task<IActionResult> ClaimCoupon(string customerId, string couponId)
    {
        await couponService.ClaimCouponAsync(customerId, couponId);
        return NoContent();
    }
}
