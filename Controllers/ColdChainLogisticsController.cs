using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;

namespace FreshGroupSystem.Controllers;

public class ColdChainLogisticsController : Controller
{
    private readonly IColdChainLogisticsService _logistics;
    private readonly IBaseRepository<LogFreightTemplate> _templates;
    private readonly IBaseRepository<LogExpressDelivery> _deliveries;

    public ColdChainLogisticsController(
        IColdChainLogisticsService logistics,
        IBaseRepository<LogFreightTemplate> templates,
        IBaseRepository<LogExpressDelivery> deliveries)
    {
        _logistics = logistics;
        _templates = templates;
        _deliveries = deliveries;
    }

    // ========== 运费模板管理 ==========

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var list = await _templates.GetAllAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateTemplate() => View(new LogFreightTemplate());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(LogFreightTemplate template)
    {
        await _templates.AddAsync(template);
        TempData["Success"] = "运费模板创建成功";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var t = await _templates.GetByIdAsync(id);
        if (t == null) { TempData["Error"] = "模板不存在"; return RedirectToAction(nameof(Index)); }
        _templates.Delete(t);
        TempData["Success"] = "模板已删除";
        return RedirectToAction(nameof(Index));
    }

    // ========== 运费报价 ==========

    [HttpGet]
    public IActionResult Quote() => View(new FreightQuoteRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(FreightQuoteRequest request)
    {
        var result = await _logistics.QuoteFreightAsync(request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            return View(request);
        }
        ViewBag.QuoteResult = result.Data;
        return View(request);
    }

    // ========== 发货管理 ==========

    [HttpGet]
    public async Task<IActionResult> Shipments()
    {
        var list = await _deliveries.GetAllAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateShipment() => View(new ShipmentRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShipment(ShipmentRequest request)
    {
        var result = await _logistics.CreateShipmentAsync(request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            return View(request);
        }
        TempData["Success"] = "发货成功，已记录批次溯源";
        return RedirectToAction(nameof(Shipments));
    }
}
