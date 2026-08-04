using Microsoft.AspNetCore.Mvc;

namespace DBFreshColdChain.Controllers
{
    public class PromotersController : Controller
    {
        public IActionResult Index()
        {
            string? promoterName = HttpContext.Session.GetString("PromoterName");
            ViewBag.Username = promoterName ?? "团长"; // 如果取不到，默认显示“团长”
            return View();
        }
    }
}
