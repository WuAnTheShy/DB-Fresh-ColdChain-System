using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Controllers;

[RequireSupplier]
public class ColdChainLogisticsController : Controller
{
    private readonly IColdChainLogisticsService _logistics;
    private readonly ILogFreightTemplateRepository _templates;
    private readonly ILogExpressDeliveryRepository _deliveries;
    private readonly IProductRepository _products;
    private readonly IGoodsRepository _goods;
    private readonly ISupplierRepository _suppliers;
    private readonly IStockBatchRepository _batches;

    public ColdChainLogisticsController(
        IColdChainLogisticsService logistics,
        ILogFreightTemplateRepository templates,
        ILogExpressDeliveryRepository deliveries,
        IProductRepository products,
        IGoodsRepository goods,
        ISupplierRepository suppliers,
        IStockBatchRepository batches)
    {
        _logistics = logistics;
        _templates = templates;
        _deliveries = deliveries;
        _products = products;
        _goods = goods;
        _suppliers = suppliers;
        _batches = batches;
    }

    // ========== 运费模板管理 ==========

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Index()
    {
        var list = await _templates.GetAllAsync();
        return View(list);
    }

    [HttpGet]
    [RequireAdmin]
    public IActionResult CreateTemplate() => View(new LogFreightTemplate());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> CreateTemplate(LogFreightTemplate template)
    {
        try
        {
            await _templates.AddAsync(template);
            TempData["Success"] = "运费模板创建成功";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"运费模板创建失败：{ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var t = await _templates.GetByIdAsync(id);
        if (t == null) { TempData["Error"] = "模板不存在"; return RedirectToAction(nameof(Index)); }
        try
        {
            _templates.Delete(t);
            TempData["Success"] = "模板已删除";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"运费模板删除失败：{ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> EditTemplate(string id)
    {
        var t = await _templates.GetByIdAsync(id);
        if (t == null) { TempData["Error"] = "模板不存在"; return RedirectToAction(nameof(Index)); }
        return View(t);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> EditTemplate(string id, LogFreightTemplate template)
    {
        if (id != template.TemplateID) { TempData["Error"] = "参数错误"; return RedirectToAction(nameof(Index)); }
        var existing = await _templates.GetByIdAsync(id);
        if (existing == null) { TempData["Error"] = "模板不存在"; return RedirectToAction(nameof(Index)); }

        existing.TemplateName = template.TemplateName;
        existing.DestinationProvince = template.DestinationProvince;
        existing.DestinationCity = template.DestinationCity;
        existing.DestinationDistrict = template.DestinationDistrict;
        existing.TemperatureZone = template.TemperatureZone;
        existing.BaseWeight = template.BaseWeight;
        existing.BaseFee = template.BaseFee;
        existing.ExtraWeightUnit = template.ExtraWeightUnit;
        existing.ExtraWeightFee = template.ExtraWeightFee;
        existing.PackagingFee = template.PackagingFee;
        existing.FreeShippingThreshold = template.FreeShippingThreshold;
        existing.IsEnabled = template.IsEnabled;

        try
        {
            _templates.Update(existing);
            TempData["Success"] = "运费模板已更新";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"运费模板更新失败：{ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    // ========== 运费报价 ==========

    [HttpGet]
    public async Task<IActionResult> Quote()
    {
        var model = new FreightQuoteRequest();
        var isAdmin = SupplierSession.IsPlatformAdmin(HttpContext.Session);
        // 普通供应商的货值按自己货物售价计算，供应商固定为自己；平台管理员可在表单指定供应商
        if (!isAdmin)
            model.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session);
        await LoadQuoteOptionsAsync(model, isAdmin);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(FreightQuoteRequest request, string? provinceText)
    {
        var isAdmin = SupplierSession.IsPlatformAdmin(HttpContext.Session);
        // 普通供应商的货值按自己货物售价计算；平台管理员可指定供应商
        if (string.IsNullOrEmpty(request.SupplierID) && !isAdmin)
            request.SupplierID = SupplierSession.GetSupplierId(HttpContext.Session);

        // 手动输入的省份优先于下拉选择
        if (!string.IsNullOrWhiteSpace(provinceText))
            request.Province = provinceText.Trim();
        ViewBag.ProvinceCustom = provinceText;

        var result = await _logistics.QuoteFreightAsync(request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            await LoadQuoteOptionsAsync(request, isAdmin);
            return View(request);
        }
        ViewBag.QuoteResult = result.Data;
        await LoadQuoteOptionsAsync(request, isAdmin);
        return View(request);
    }

    // ========== 报价页下拉数据 ==========

    /// <summary>加载报价表单的下拉选项：商品（自有置顶）、供应商（仅管理员）、目的省份（来自启用模板）</summary>
    private async Task LoadQuoteOptionsAsync(FreightQuoteRequest model, bool isAdmin)
    {
        ViewBag.IsAdmin = isAdmin;

        // 商品：全部商品；普通供应商自己的货物置顶并标注"(自有)"
        var allProducts = await _products.GetAllAsync();
        var ownIds = new HashSet<string>();
        if (!isAdmin && !string.IsNullOrWhiteSpace(model.SupplierID))
        {
            var ownGoods = await _goods.GetBySupplierAsync(model.SupplierID);
            ownIds = ownGoods.Select(g => g.ProductID).ToHashSet();
        }
        ViewBag.ProductOptions = allProducts
            .OrderBy(p => ownIds.Contains(p.ProductID) ? 0 : 1)
            .ThenBy(p => p.ProductName)
            .Select(p => new OptionItemDto
            {
                Value = p.ProductID,
                Text = ownIds.Contains(p.ProductID) ? $"{p.ProductName}（自有）" : p.ProductName
            }).ToList();

        // 供应商：仅管理员可选，下拉列出全部供应商
        ViewBag.SupplierOptions = isAdmin
            ? (await _suppliers.GetAllAsync())
                .OrderBy(s => s.SupplierName)
                .Select(s => new OptionItemDto { Value = s.SupplierID, Text = s.SupplierName })
                .ToList()
            : new List<OptionItemDto>();

        // 目的省份：启用的运费模板去重（与报价匹配逻辑同源）
        var templates = await _templates.GetEnabledAsync();
        ViewBag.ProvinceOptions = templates
            .Select(t => t.DestinationProvince)
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "*")
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        ViewBag.HasWildcardProvince = templates.Any(t =>
            string.IsNullOrWhiteSpace(t.DestinationProvince) || t.DestinationProvince == "*");
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
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Shipments));
    }

    // ========== 精准溯源查询 ==========

    /// <summary>溯源查询入口页</summary>
    [HttpGet]
    public async Task<IActionResult> Traceability()
    {
        await LoadTraceOptionsAsync();
        return View();
    }

    // ========== 溯源页下拉数据 ==========

    /// <summary>加载溯源查询的下拉选项：订单（有发货记录）、发货单（全部）、批次（全部）</summary>
    private async Task LoadTraceOptionsAsync()
    {
        // 订单：只列有发货记录的订单（DISTINCT OrderID JOIN 订单号）
        ViewBag.OrderOptions = (await _deliveries.GetDistinctOrdersAsync())
            .Select(o => new OptionItemDto
            {
                Value = o.OrderID,
                Text = string.IsNullOrWhiteSpace(o.OrderNo) ? $"订单 {o.OrderID[..8]}" : o.OrderNo
            }).ToList();

        // 发货单：全部（与发货记录页一致）
        ViewBag.DeliveryOptions = (await _deliveries.GetAllAsync())
            .OrderByDescending(d => d.ShippedAt)
            .Select(d => new OptionItemDto
            {
                Value = d.DeliveryID,
                Text = $"{d.TrackingNo}（{d.DeliveryID[..8]}）"
            }).ToList();

        // 批次：全部批次（反向溯源允许查任意批次，不做 ACTIVE/库存过滤）
        var batches = await _batches.GetAllAsync();
        var productMap = (await _products.GetAllAsync()).ToDictionary(p => p.ProductID);
        ViewBag.BatchOptions = batches
            .OrderByDescending(b => b.ProductionDate)
            .ThenBy(b => b.BatchNo)
            .Select(b => new OptionItemDto
            {
                Value = b.BatchID,
                Text = productMap.TryGetValue(b.ProductID, out var p)
                    ? $"{b.BatchNo}（{p.ProductName}）" : b.BatchNo
            }).ToList();
    }

    /// <summary>按订单 ID 查询完整溯源链路</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            ViewBag.Error = "请选择订单";
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        var result = await _logistics.GetTraceabilityByOrderAsync(orderId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        ViewBag.TraceResult = result.Data;
        ViewBag.QueryType = "order";
        ViewBag.QueryKey = orderId;
        await LoadTraceOptionsAsync();
        return View("Traceability");
    }

    /// <summary>按发货单 ID 查询单张发货单的批次明细</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByDelivery(string deliveryId)
    {
        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            ViewBag.Error = "请选择发货单";
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        var result = await _logistics.GetTraceabilityByDeliveryAsync(deliveryId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        ViewBag.TraceResult = new List<DeliveryTraceDto> { result.Data! };
        ViewBag.QueryType = "delivery";
        ViewBag.QueryKey = deliveryId;
        await LoadTraceOptionsAsync();
        return View("Traceability");
    }

    /// <summary>反向溯源：按批次 ID 查去向</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByBatch(string batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            ViewBag.Error = "请选择批次";
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        var result = await _logistics.GetBatchTraceAsync(batchId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            await LoadTraceOptionsAsync();
            return View("Traceability");
        }
        ViewBag.BatchTrace = result.Data;
        ViewBag.QueryType = "batch";
        ViewBag.QueryKey = batchId;
        await LoadTraceOptionsAsync();
        return View("Traceability");
    }
}
