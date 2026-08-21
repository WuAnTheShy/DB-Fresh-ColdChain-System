//用于前端页面API

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Models;

namespace FreshColdChain.Controllers;

public class SuppliersHomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
