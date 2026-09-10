using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

// 动态定价引擎 — 价格规则管理与实时价格计算
[RequireSupplier]
public class PricesController : Controller
{
    private readonly IPricingService _pricing;
    private readonly IPromoterListedPriceSyncService _listedPriceSync;

    public PricesController(IPricingService pricing, IPromoterListedPriceSyncService listedPriceSync)
    {
        _pricing = pricing;
        _listedPriceSync = listedPriceSync;
    }

    // 规则列表

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 15)
    {
        var r = await _pricing.GetAllRulesAsync(pageIndex, pageSize, ScopedSupplierId());
        return View(r.Data);
    }

    // 创建规则

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var dto = new SavePriceRuleDto();
        if (ScopedSupplierId() is { } supplierId)
            dto.SupplierID = supplierId; // 平台管理员不预设，需在下拉中选定所属供应商
        await LoadGoodsOptionsAsync(ScopedSupplierId());
        return View(dto);
    }

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
            await LoadGoodsOptionsAsync(ScopedSupplierId());
            return View(dto);
        }
        TempData["Success"] = r.Message + await BuildSyncSuffixAsync(new[] { (dto.SupplierID, dto.ProductID) });
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
        var dto = new SavePriceRuleDto
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
        };
        await LoadGoodsOptionsAsync(ScopedSupplierId());
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SavePriceRuleDto dto)
    {
        // 普通供应商的规则归属保持自己，防止把规则改挂到别的供应商下
        if (!SupplierSession.IsPlatformAdmin(HttpContext.Session))
            dto.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session);
        // 变更前的归属/商品：平台管理员把规则迁移到别的供应商时，新旧供应商名下的已上架条目都要重算
        var before = (await _pricing.GetRuleByIdAsync(id, ScopedSupplierId())).Data;
        var r = await _pricing.UpdateRuleAsync(id, dto, ScopedSupplierId());
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            await LoadGoodsOptionsAsync(ScopedSupplierId());
            return View(dto);
        }
        var scopes = new List<(string? SupplierId, string ProductId)> { (dto.SupplierID, dto.ProductID) };
        if (before != null
            && !string.Equals(before.SupplierID, dto.SupplierID, StringComparison.OrdinalIgnoreCase))
        {
            scopes.Add((before.SupplierID, before.ProductID));
        }
        TempData["Success"] = r.Message + await BuildSyncSuffixAsync(scopes);
        return RedirectToAction(nameof(Index));
    }

    // 删除规则

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        // 删除前取规则归属的商品×供应商，删除成功后其名下已上架条目需重算
        var before = (await _pricing.GetRuleByIdAsync(id, ScopedSupplierId())).Data;
        var r = await _pricing.DeleteRuleAsync(id, ScopedSupplierId());
        var message = r.Message;
        if (r.IsSuccess && before != null)
            message += await BuildSyncSuffixAsync(new[] { (before.SupplierID, before.ProductID) });
        TempData[r.IsSuccess ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    // 实时价格计算

    [HttpGet]
    public async Task<IActionResult> Calculate()
    {
        await LoadGoodsOptionsAsync(ScopedSupplierId());
        return View(new PriceCalculationRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(PriceCalculationRequest request)
    {
        // 普通供应商算自己货物售价；平台管理员（原供应商管理员，现商品管理员）需在表单指定供应商
        if (!SupplierSession.IsPlatformAdmin(HttpContext.Session))
            request.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session) ?? "";
        await LoadGoodsOptionsAsync(ScopedSupplierId());
        var r = await _pricing.CalculatePriceAsync(request);
        if (!r.IsSuccess)
        {
            ModelState.AddModelError("", r.Message);
            return View(request);
        }
        ViewBag.Result = r.Data;
        return View(request);
    }

    // 当前供应商 ID：平台管理员返回 null（可操作全部），普通供应商返回自己的 ID
    private string? ScopedSupplierId()
        => SupplierSession.IsPlatformAdmin(HttpContext.Session) ? null : SupplierSession.GetSupplierId(HttpContext.Session);

    // 加载「货物商品」下拉选项到 ViewData（新建/编辑规则与实时试算共用）。
    // scopedSupplierId：普通供应商传自己，平台管理员传 null 可选全部货物。
    private async Task LoadGoodsOptionsAsync(string? scopedSupplierId)
    {
        ViewData["GoodsOptions"] = await _pricing.GetGoodsOptionsAsync(scopedSupplierId);
        // 平台管理员（无固定供应商）需要在下拉里体现供应商归属
        ViewData["IsAdminScope"] = scopedSupplierId == null;
    }

    // 规则增删改成功后，同步重算对应（供应商×商品）下所有团长已上架条目的动态定价快照，
    // 并返回“已同步更新 N 个……”的中文提示后缀；同步失败不阻断规则保存，只给出提示。
    private async Task<string> BuildSyncSuffixAsync(IEnumerable<(string? SupplierId, string ProductId)> scopes)
    {
        var total = 0;
        var failed = 0;
        foreach (var (supplierId, productId) in scopes)
        {
            if (string.IsNullOrWhiteSpace(supplierId))
                continue;
            try
            {
                total += await _listedPriceSync.SyncListedPricesAsync(supplierId, productId);
            }
            catch
            {
                failed++;
            }
        }

        var parts = new List<string>();
        if (total > 0)
            parts.Add($"已同步重算 {total} 个团长已上架商品的供应商动态定价");
        if (failed > 0)
            parts.Add($"{failed} 个同步失败，相关商品重新入团后可刷新价格");
        return parts.Count > 0 ? "，" + string.Join("，", parts) : "";
    }
}
