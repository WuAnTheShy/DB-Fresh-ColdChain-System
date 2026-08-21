using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers
{
    public class AdminsController : Controller
    {
        private readonly PromoterService _promoterService;
        private readonly SystemAdminService _systemAdminService;
        private readonly WithdrawalService _withdrawalService;
        public AdminsController(PromoterService promoterService, SystemAdminService systemAdminService, WithdrawalService withdrawalService)
        {
            _promoterService = promoterService;
            _systemAdminService = systemAdminService;
            _withdrawalService = withdrawalService;
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
    }
}

