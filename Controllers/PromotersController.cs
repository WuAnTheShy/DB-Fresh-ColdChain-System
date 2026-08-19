using DBFreshColdChain.Models.ViewModels;
using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers
{
    public class PromotersController : Controller
    {
        private const string SessionPromoterIdKey = "PromoterId";
        private const string SessionPromoterNameKey = "PromoterName";
        private readonly PromoterPortalDataProvider _dataProvider;

        public PromotersController(PromoterPortalDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
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
        public IActionResult Withdrawals(WithdrawalApplyForm form)
        {
            var redirect = EnsureLoggedIn();
            if (redirect != null) return redirect;
            var promoterId = GetPromoterId()!;
            var (success, message) = _dataProvider.ApplyWithdrawal(promoterId, form);
            if (success)
                TempData["SuccessMessage"] = message;
            else
                TempData["ErrorMessage"] = message;
            return View(_dataProvider.BuildWithdrawals(promoterId, form));
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
    }
}
