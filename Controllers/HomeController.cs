//用于前端页面API

using Microsoft.AspNetCore.Mvc;

namespace FreshGroupSystem.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
