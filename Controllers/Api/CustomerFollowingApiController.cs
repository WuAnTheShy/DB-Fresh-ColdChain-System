using FreshColdChain.Interfaces;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

// 消费者关注团长关系 API，持久化到 CRM_PCR。
[ApiController]
[Route("api/customers/{customerId}/following")]
public sealed class CustomerFollowingApiController : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetFollowing(
        string customerId,
        [FromServices] IPromoterService promoterService)
    {
        var authError = AuthorizeCustomer(customerId);
        if (authError != null) return authError;

        var promoterIds = await promoterService.GetBoundPromoterIdsAsync(customerId);
        return Ok(new { promoterIds });
    }

    [HttpPost("{promoterId}")]
    public async Task<IActionResult> Follow(
        string customerId,
        string promoterId,
        [FromServices] IPromoterService promoterService,
        CancellationToken cancellationToken)
    {
        var authError = AuthorizeCustomer(customerId);
        if (authError != null) return authError;

        var result = await promoterService.BindCustomerToPromoterAsync(
            customerId,
            promoterId,
            cancellationToken: cancellationToken);
        if (!result.IsSuccess)
            throw new GroupBBusinessException(NormalizeError(result.ErrorMessage, "关注团长失败"));

        return NoContent();
    }

    [HttpDelete("{promoterId}")]
    public async Task<IActionResult> Unfollow(
        string customerId,
        string promoterId,
        [FromServices] IPromoterService promoterService,
        CancellationToken cancellationToken)
    {
        var authError = AuthorizeCustomer(customerId);
        if (authError != null) return authError;

        var result = await promoterService.UnbindCustomerFromPromoterAsync(
            customerId,
            promoterId,
            cancellationToken);
        if (!result.IsSuccess)
            throw new GroupBBusinessException(NormalizeError(result.ErrorMessage, "取消关注失败"));

        return NoContent();
    }

    private static string NormalizeError(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        return message.StartsWith("系统错误：", StringComparison.Ordinal)
            ? message[5..]
            : message;
    }
}
