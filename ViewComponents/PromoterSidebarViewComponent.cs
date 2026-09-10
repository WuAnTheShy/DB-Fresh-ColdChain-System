using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.ViewComponents
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
                CurrentBalance = promoter.CurrentBalance
            });
        }
    }

    public class PromoterSidebarModel
    {
        // 可提现余额（团长身份 / 基础佣金 / 累计销售额已移至侧栏顶部身份卡）
        public decimal CurrentBalance { get; set; }
    }
}
