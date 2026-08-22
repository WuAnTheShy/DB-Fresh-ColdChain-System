using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers
{
    public class AdminsController : Controller
    {
        private readonly PromoterService _promoterService;
        private readonly SystemAdminService _systemAdminService;
        private readonly WithdrawalService _withdrawalService;
        private readonly IRefundService _refundService;
        public AdminsController(PromoterService promoterService, SystemAdminService systemAdminService, WithdrawalService withdrawalService, IRefundService refundService)
        {
            _promoterService = promoterService;
            _systemAdminService = systemAdminService;
            _withdrawalService = withdrawalService;
            _refundService = refundService;
        }

        public async Task<IActionResult> PendingPromoters()
        {
            var pendingList = await _promoterService.GetPendingPromotersAsync();
            return View(pendingList);
        }
        // 管理员首页
        public async Task<IActionResult> Dashboard()
        {
            var adminName = HttpContext.Session.GetString("AdminName");
            ViewBag.AdminName = adminName ?? "管理员";
            var pendingPromoters = await _promoterService.GetPendingPromotersAsync();
            ViewBag.PendingPromoterCount = pendingPromoters.Count;

            var pendingWithdrawals = await _withdrawalService.GetPendingWithdrawalsAsync();
            ViewBag.PendingWithdrawalCount = pendingWithdrawals.Count;

            var pendingRefunds = await _refundService.GetPendingRefundsAsync();
            ViewBag.PendingRefundCount = pendingRefunds.Count;
            return View();
        }
        // 团长审核通过
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePromoter(string promoterId)
        {
            string adminId = HttpContext.Session.GetString("AdminName") ?? "System";
            var result = await _systemAdminService.ApprovePromoterAsync(promoterId, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMsg"] = "审核通过成功！";
            }
            else
            {
                TempData["ErrorMsg"] = result.ErrorMessage;
            }
            return RedirectToAction(nameof(PendingPromoters));
        }

        // 团长审核拒绝
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPromoter(string promoterId)
        {
            string adminId = HttpContext.Session.GetString("AdminName") ?? "System";
            var result = await _systemAdminService.RejectPromoterAsync(promoterId, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMsg"] = "审核拒绝成功！";
            }
            else
            {
                TempData["ErrorMsg"] = result.ErrorMessage;
            }
            return RedirectToAction(nameof(PendingPromoters));
        }

        public async Task<IActionResult> PendingWithdrawals()
        {
            var list = await _withdrawalService.GetPendingWithdrawalsAsync();
            return View(list);
        }



        // 提现审核通过
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveWithdrawal(string withdrawalId)
        {
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _withdrawalService.ApproveWithdrawal(adminId, new GroupC_WithdrawApproved
            {
                WithdrawalId = withdrawalId,
                UserId = adminId,
                AuditTime = DateTime.Now
            });
            TempData[result.IsSuccess ? "SuccessMsg" : "ErrorMsg"] = result.ErrorMessage;
            return RedirectToAction(nameof(PendingWithdrawals));
        }

        // 提现驳回（需要输入原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectWithdrawal(string withdrawalId, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMsg"] = "请填写驳回原因";
                return RedirectToAction(nameof(PendingWithdrawals));
            }
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _withdrawalService.RejectWithdrawal(adminId, new GroupC_WithdrawRejected
            {
                WithdrawalId = withdrawalId,
                UserId = adminId,
                AuditTime = DateTime.Now,
                RejectReason = rejectReason
            });
            TempData[result.IsSuccess ? "SuccessMsg" : "ErrorMsg"] = result.ErrorMessage;
            return RedirectToAction(nameof(PendingWithdrawals));
        }

        // 退款审核列表
        public async Task<IActionResult> PendingRefunds()
        {
            var list = await _refundService.GetPendingRefundsAsync();
            return View(list);
        }

        // 退款审核通过：执行退款资金操作（佣金/积分回滚、订单状态变更）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(string refundId)
        {
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _refundService.AuditRefund(refundId, true, adminId);
            TempData[result.IsSuccess ? "SuccessMsg" : "ErrorMsg"] =
                result.IsSuccess ? "退款已通过，资金回滚已执行" : result.ErrorMessage;
            return RedirectToAction(nameof(PendingRefunds));
        }

        // 退款审核驳回（需要输入原因）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(string refundId, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMsg"] = "请填写驳回原因";
                return RedirectToAction(nameof(PendingRefunds));
            }
            var adminId = HttpContext.Session.GetString("AdminName") ?? "Admin";
            var result = await _refundService.AuditRefund(refundId, false, adminId, rejectReason);
            TempData[result.IsSuccess ? "SuccessMsg" : "ErrorMsg"] =
                result.IsSuccess ? "退款申请已驳回" : result.ErrorMessage;
            return RedirectToAction(nameof(PendingRefunds));
        }
    }
}

