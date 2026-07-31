using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;

namespace FreshGroupSystem.Controllers;

public class LogisticsController : Controller
{
    private readonly ILogisticsService _service;

    public LogisticsController(ILogisticsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _service.GetPendingDeliveryOrdersAsync();
        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDelivery(int orderId)
    {
        var result = await _service.StartDeliveryAsync(orderId);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDelivery(int orderId)
    {
        var result = await _service.CompleteDeliveryAsync(orderId);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Summary(int leaderId)
    {
        var result = await _service.GetDeliverySummaryByLeaderAsync(leaderId);
        ViewBag.LeaderId = leaderId;
        return View(result.Data);
    }
}
