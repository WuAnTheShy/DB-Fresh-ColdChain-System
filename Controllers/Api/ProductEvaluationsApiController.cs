using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api")]
public sealed class ProductEvaluationsApiController(
    IProductEvaluationService evaluationService) : GroupBApiController
{
    [HttpPost("orders/{orderId}/items/{orderDetailId}/evaluation")]
    public async Task<IActionResult> Submit(
        string orderId,
        string orderDetailId,
        ProductEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();

        await evaluationService.SubmitAsync(
            orderId, orderDetailId, customerId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { message = "评价已提交" });
    }

    [HttpPost("orders/{orderId}/evaluation")]
    public async Task<IActionResult> SubmitOrder(
        string orderId,
        ProductEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();

        await evaluationService.SubmitOrderAsync(
            orderId, customerId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { message = "订单评价已提交" });
    }

    [HttpGet("promoters/{promoterId}/evaluation-summary")]
    public async Task<IActionResult> GetSummary(string promoterId)
    {
        return Ok(await evaluationService.GetSummaryAsync(promoterId));
    }
}
