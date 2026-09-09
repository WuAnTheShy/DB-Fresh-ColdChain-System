using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

/// <summary>
/// 动态定价引擎 — 价格规则管理与实时价格计算
/// </summary>
[RequireSupplier]
public class PricesController : Controller
{
    private readonly IPricingService _pricing;

    public PricesController(IPricingService pricing) => _pricing = pricing;

    // 规则列表

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 15)
    {
        var r = await _pricing.GetAllRulesAsync(pageIndex, pageSize, ScopedSupplierId());
        return View(r.Data);
    }

    // 创建规则

    [HttpGet]
    public IActionResult Create() => View(new SavePriceRuleDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SavePriceRuleDto dto)
    {
        // 普通供应商的规则强制归属自己；平台管理员（原供应商管理员，现商品管理员）可指定任意供应商
        if (!SupplierSession.IsPlatformAdmin(HttpContext.Session))
            dto.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session);
        var r = await _pricing.CreateRuleAsync(dto);
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            return View(dto);
        }
        TempData["Success"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // 编辑规则

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var r = await _pricing.GetRuleByIdAsync(id, ScopedSupplierId());
        if (!r.IsSuccess)
        {
            TempData["Error"] = r.Message;
            return RedirectToAction(nameof(Index));
        }

        var rule = r.Data!;
        return View(new SavePriceRuleDto
        {
            ProductID = rule.ProductID,
            SupplierID = rule.SupplierID,
            RuleName = rule.RuleName,
            TriggerType = rule.TriggerType,
            TimeWindow = rule.TimeWindow,
            DiscountRate = rule.DiscountRate,
            ManualPrice = rule.ManualPrice,
            MinQuantity = rule.MinQuantity,
            MaxQuantity = rule.MaxQuantity,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SavePriceRuleDto dto)
    {
        var r = await _pricing.UpdateRuleAsync(id, dto, ScopedSupplierId());
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            return View(dto);
        }
        TempData["Success"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // 删除规则

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _pricing.DeleteRuleAsync(id, ScopedSupplierId());
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // 实时价格计算

    [HttpGet]
    public IActionResult Calculate() => View(new PriceCalculationRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(PriceCalculationRequest request)
    {
        // 普通供应商算自己货物售价；平台管理员（原供应商管理员，现商品管理员）需在表单指定供应商
        if (!SupplierSession.IsPlatformAdmin(HttpContext.Session))
            request.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session) ?? "";
        var r = await _pricing.CalculatePriceAsync(request);
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            return View(request);
        }
        ViewBag.Result = r.Data;
        return View(request);
    }

    /// <summary>当前供应商 ID：平台管理员返回 null（可操作全部），普通供应商返回自己的 ID</summary>
    private string? ScopedSupplierId()
        => SupplierSession.IsPlatformAdmin(HttpContext.Session) ? null : SupplierSession.GetSupplierId(HttpContext.Session);
}
