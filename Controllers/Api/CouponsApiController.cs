using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/customers/{customerId:int}/coupons")]
public sealed class CouponsApiController(
    ICouponService couponService) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetCouponCenter(int customerId)
    {
        var result = await couponService.GetCouponCenterAsync(customerId);
        return result == null
            ? ApiNotFound("消费者不存在")
            : Ok(result);
    }

    [HttpPost("{couponId:int}/claim")]
    public async Task<IActionResult> ClaimCoupon(int customerId, int couponId)
    {
        await couponService.ClaimCouponAsync(customerId, couponId);
        return NoContent();
    }
}
