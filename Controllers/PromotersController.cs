using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.ViewModels;
using DBFreshColdChain.Services;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers
{
    public class PromotersController : Controller
    {
        private const string SessionPromoterIdKey = "PromoterId";
        private const string SessionPromoterNameKey = "PromoterName";
        private readonly PromoterPortalDataProvider _dataProvider;
        private readonly ISupplierService _supplierService;
        private readonly PromoterService _promoterService;
        private readonly WithdrawalService _withdrawalService;  

        public PromotersController(
            PromoterPortalDataProvider dataProvider,
            ISupplierService supplierService,
            PromoterService promoterService,
            WithdrawalService withdrawalService)
        { 
            _dataProvider = dataProvider;
            _supplierService = supplierService;
            _promoterService = promoterService;
            _withdrawalService = withdrawalService;
        }

        public IActionResult Dashboard()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildDashboard(GetPromoterId()!));
        }


        public IActionResult Performance()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoter = _dataProvider.GetPromoter(GetPromoterId()!);
            return View(_dataProvider.BuildPerformance(promoter));
        }

        public IActionResult Commissions(string? status)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildCommissions(GetPromoterId()!, status));
        }

        [HttpGet]
        public IActionResult Withdrawals()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildWithdrawals(GetPromoterId()!));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdrawals(WithdrawalApplyForm form)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;

            var request = new GroupC_WithdrawalRequest
            {
                PromoterId = promoterId,
                ApplyAmount = form.ApplyAmount,
                AccountInfo = $"{form.AccountPlatform}：{form.AccountInfo}"
            };

            var result = await _withdrawalService.ApplyWithdrawal(promoterId, request);
            if (result.IsSuccess)
                TempData["SuccessMessage"] = "提现申请已提交，请等待审核。";
            else
                TempData["ErrorMessage"] = result.ErrorMessage;

            var vm = _dataProvider.BuildWithdrawals(promoterId, form);
            return View(vm);
        }

        public IActionResult Profile()
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            return View(_dataProvider.BuildProfile(GetPromoterId()!));
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SessionPromoterIdKey);
            HttpContext.Session.Remove(SessionPromoterNameKey);
            return RedirectToAction("RoleSelect", "Account");
        }

        private string? GetPromoterId() => HttpContext.Session.GetString(SessionPromoterIdKey);

        private IActionResult? EnsureLoggedIn()
        {
            if (string.IsNullOrEmpty(GetPromoterId()))
                return RedirectToAction("Login", "Account", new { role = "团长" });
            return null;
        }


        // ========= 新增：供应商绑定页 =========

        /// <summary>
        /// 展示供应商绑定/搜索页面
        /// </summary>
        [HttpGet]
        public IActionResult BindSupplier()
        {
            // 初始化一个空的 ViewModel
            var model = new SupplierSearchViewModel
            {
                Keyword = "",
                Suppliers = new List<SupplierAccountDto>(),
                BoundSupplierIds = new List<string>()
            };
            return View(model);
        }

        /// <summary>
        /// 搜索供应商 (调用 A 组接口)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SearchSupplier(string keyword)
        {
            var promoterId = HttpContext.Session.GetString("PromoterId");
            var model = new SupplierSearchViewModel { Keyword = keyword };

            // 1. 调用 A 组的接口搜索供应商
            var response = await _supplierService.FindSupplierAccountAsync(
                supplierName: keyword // 您也可以通过 supplierId 传参，这里用名称搜索
            );

            if (response.IsSuccess)
            {
                model.Suppliers = response.Data ?? new List<SupplierAccountDto>();
            }
            else
            {
                TempData["ErrorMessage"] = "搜索失败：" + response.Message;
                model.Suppliers = new List<SupplierAccountDto>();
            }

            // 2. 获取当前团长已经绑定的供应商 ID 列表，用于前端判断显示“绑定”还是“解绑”
            model.BoundSupplierIds = await _promoterService.GetActiveSupplierIdsAsync(promoterId);

            return View("BindSupplier", model);
        }

        /// <summary>
        /// 绑定或解绑供应商
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleRelation(string supplierId, string action)
        {
            var promoterId = HttpContext.Session.GetString("PromoterId");
            bool success = false;

            try
            {
                if (action == "bind")
                {
                    success = await _promoterService.AddRelationAsync(promoterId, supplierId);
                    TempData["SuccessMessage"] = success ? "绑定成功！" : "绑定失败，请重试。";
                }
                else if (action == "unbind")
                {
                    success = await _promoterService.RemoveRelationAsync(promoterId, supplierId);
                    TempData["SuccessMessage"] = success ? "已解绑！" : "解绑失败，请重试。";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "操作发生异常：" + ex.Message;
            }

            // 操作完成后，重新跳转回绑定页（如果需要保留搜索结果，可以带上 keyword）
            return RedirectToAction("BindSupplier");
        }
    }







}

