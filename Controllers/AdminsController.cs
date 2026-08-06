using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers
{
    public class AdminsController : Controller
    {
        private readonly PromoterService _promoterService;
        private readonly SystemAdminService _systemAdminService;
        public AdminsController(PromoterService promoterService, SystemAdminService systemAdminService)
        {
            _promoterService = promoterService;
            _systemAdminService = systemAdminService;
        }
        public IActionResult Index()
        {
            string? adminName = HttpContext.Session.GetString("AdminName");
            ViewBag.Username = adminName ?? "管理员"; // 如果取不到，默认显示“管理员”
            return View();
        }
        public async Task<IActionResult> PendingPromoters()
        {
            var pendingList = await _promoterService.GetPendingPromotersAsync();
            return View(pendingList);
        }

        // 审核通过
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

        // 审核拒绝
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
    }
}
