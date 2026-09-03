using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/orders/{orderId}/refunds")]
public sealed class RefundsApiController(
    IRefundService refundService,
    IOrderService orderService) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetRefunds(string orderId)
    {
        var authorization = await AuthorizeOrderAsync(orderId);
        if (authorization != null) return authorization;

        var records = await refundService.GetOrderRefundsAsync(orderId);
        return Ok(new
        {
            refunds = records.Select(record => new
            {
                record.RefundId,
                record.OrderId,
                record.DetailId,
                record.RefundQty,
                record.RefundAmount,
                record.ApplyTime,
                record.Remark,
                record.Status,
                record.AuditTime
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> ApplyRefund(
        string orderId,
        GroupC_RefundRequest request)
    {
        var authorization = await AuthorizeOrderAsync(orderId);
        if (authorization != null) return authorization;
        if (request.Remark?.Trim().Length is 0 or > 200)
            return ApiBadRequest("请填写200字以内的退款原因");

        request.OrderId = orderId;
        request.DetailId = null;
        request.ProductID = string.IsNullOrWhiteSpace(request.ProductID)
            ? null
            : request.ProductID.Trim();
        request.Items = (request.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductID))
            .Select(item => new GroupC_RefundItemRequest
            {
                ProductID = item.ProductID.Trim(),
                RefundQty = item.RefundQty
            })
            .ToList();
        request.LiabilityType = "Customer";
        request.Remark = request.Remark?.Trim();
        var result = await refundService.ApplyRefund(request);
        if (!result.IsSuccess)
            return ApiBadRequest(result.ErrorMessage ?? "退款申请提交失败");

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "退款申请已提交，请等待平台审核"
        });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewRefund(
        string orderId,
        GroupC_RefundRequest request)
    {
        var authorization = await AuthorizeOrderAsync(orderId);
        if (authorization != null) return authorization;

        NormalizeRequest(orderId, request);
        var result = await refundService.PreviewRefundAsync(request);
        return result.IsSuccess
            ? Ok(new
            {
                result.RefundAmount,
                result.GoodsRefundAmount,
                result.FreightRefundAmount
            })
            : ApiBadRequest(result.ErrorMessage ?? "退款金额试算失败");
    }

    [HttpDelete("{refundId}")]
    public async Task<IActionResult> CancelRefund(string orderId, string refundId)
    {
        var authorization = await AuthorizeOrderAsync(orderId);
        if (authorization != null) return authorization;

        var result = await refundService.CancelRefundApplicationAsync(
            orderId,
            refundId,
            SignedInCustomerId!);
        return result.IsSuccess
            ? Ok(new { message = "退款申请已取消，可重新选择未退款商品" })
            : ApiBadRequest(result.ErrorMessage ?? "取消退款申请失败");
    }

    private async Task<IActionResult?> AuthorizeOrderAsync(string orderId)
    {
        var customerId = SignedInCustomerId;
        if (string.IsNullOrWhiteSpace(customerId)) return ApiUnauthorized();
        var detail = await orderService.GetOrderDetailAsync(orderId);
        if (detail?.Order == null) return ApiNotFound("订单不存在");
        return string.Equals(detail.Order.CustomerId, customerId, StringComparison.Ordinal)
            ? null
            : ApiForbidden();
    }


    private static void NormalizeRequest(string orderId, GroupC_RefundRequest request)
    {
        request.OrderId = orderId;
        request.DetailId = null;
        request.ProductID = string.IsNullOrWhiteSpace(request.ProductID)
            ? null
            : request.ProductID.Trim();
        request.Items = (request.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductID))
            .Select(item => new GroupC_RefundItemRequest
            {
                ProductID = item.ProductID.Trim(),
                RefundQty = item.RefundQty
            })
            .ToList();
        request.LiabilityType = "Customer";
        request.Remark = request.Remark?.Trim();
    }
}
