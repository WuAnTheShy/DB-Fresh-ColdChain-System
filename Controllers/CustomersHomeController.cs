using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Models;

namespace FreshColdChain.Controllers;

public class CustomersHomeController : Controller
{
    public IActionResult Index()
    {
        return Redirect("/app/");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
