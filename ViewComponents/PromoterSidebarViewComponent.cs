using DBFreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.ViewComponents
{
    public class PromoterSidebarViewComponent : ViewComponent
    {
        private readonly PromoterPortalDataProvider _dataProvider;

        public PromoterSidebarViewComponent(PromoterPortalDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        public IViewComponentResult Invoke()
        {
            var promoterId = HttpContext.Session.GetString("PromoterId");
            if (string.IsNullOrEmpty(promoterId))
                return Content(string.Empty);

            var promoter = _dataProvider.GetPromoter(promoterId);
            return View(new PromoterSidebarModel
            {
                PromoterName = HttpContext.Session.GetString("PromoterName") ?? promoter.PromoterName,
                CurrentBalance = promoter.CurrentBalance,
                LevelName = PromoterPortalDataProvider.ResolveLevelName(promoter.TotalSales)
            });
        }
    }

    public class PromoterSidebarModel
    {
        public string PromoterName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public string LevelName { get; set; } = string.Empty;
    }
}
