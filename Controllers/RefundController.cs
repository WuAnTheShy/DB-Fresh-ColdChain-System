using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers
{
    /// <summary>
    /// 消费者端退款申请入口。退款审核与资金操作由 C 组 IRefundService 完成。
    /// </summary>
    public class RefundController : Controller
    {
        private readonly IRefundService _refundService;
        private readonly IOrderService _orderService;
        private readonly ILogger<RefundController> _logger;

        public RefundController(
            IRefundService refundService,
            IOrderService orderService,
            ILogger<RefundController> logger)
        {
            _refundService = refundService;
            _orderService = orderService;
            _logger = logger;
        }

        // 退款申请页：展示订单信息、可选商品与历史申请记录
        [HttpGet]
        public async Task<IActionResult> Apply(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return BadRequest();

            try
            {
                var order = await _orderService.GetOrderDetailAsync(orderId);
                if (order?.Order == null)
                    return NotFound();

                var model = new GroupC_RefundApplyViewModel
                {
                    Order = order,
                    ExistingRefunds = await _refundService.GetOrderRefundsAsync(orderId)
                };
                return View(model);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "加载退款申请页失败，订单 {OrderId}", orderId);
                TempData["ErrorMessage"] = "系统暂时无法加载退款申请页，请稍后重试";
                return RedirectToAction("Detail", "Order", new { id = orderId });
            }
        }

        // 提交退款申请：仅创建待审核申请单，等待管理员审核
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(GroupC_RefundRequest request)
        {
            try
            {
                var result = await _refundService.ApplyRefund(request);
                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = "退款申请已提交，请等待平台审核";
                }
                else
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "提交退款申请失败，订单 {OrderId}", request.OrderId);
                TempData["ErrorMessage"] = "系统暂时无法提交退款申请，请稍后重试";
            }
            return RedirectToAction("Detail", "Order", new { id = request.OrderId });
        }
    }
}
