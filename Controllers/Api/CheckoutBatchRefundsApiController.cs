using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/orders/batches/{checkoutBatchId}/refunds")]
public sealed class CheckoutBatchRefundsApiController(IRefundService refundService) : GroupBApiController
{
    [HttpPost]
    public async Task<IActionResult> Apply(string checkoutBatchId, CheckoutBatchRefundRequest request)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();
        if (string.IsNullOrWhiteSpace(checkoutBatchId)) return ApiBadRequest("结算批次不能为空");
        var result = await refundService.ApplyCheckoutBatchRefundAsync(
            checkoutBatchId, customerId, request.Remark.Trim());
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { message = "整个结算批次的退款申请已提交，等待平台分别审核" })
            : ApiBadRequest(result.ErrorMessage ?? "退款申请提交失败");
    }
}
