using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Controllers;

public class ColdChainLogisticsController : Controller
{
    private readonly IColdChainLogisticsService _logistics;
    private readonly ILogFreightTemplateRepository _templates;
    private readonly ILogExpressDeliveryRepository _deliveries;
    private readonly IProductRepository _products;

    public ColdChainLogisticsController(
        IColdChainLogisticsService logistics,
        ILogFreightTemplateRepository templates,
        ILogExpressDeliveryRepository deliveries,
        IProductRepository products)
    {
        _logistics = logistics;
        _templates = templates;
        _deliveries = deliveries;
        _products = products;
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
    public async Task<IActionResult> EditTemplate(string id)
    {
        var t = await _templates.GetByIdAsync(id);
        if (t == null) { TempData["Error"] = "模板不存在"; return RedirectToAction(nameof(Index)); }
        return View(t);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
    public IActionResult Quote() => View(new FreightQuoteRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(FreightQuoteRequest request)
    {
        // 根据商品明细自动计算货值总额
        decimal goodsAmount = 0;
        foreach (var item in request.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductID);
            if (product != null)
                goodsAmount += product.DefaultPrice * item.Quantity;
        }
        request.GoodsAmount = goodsAmount;

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
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Shipments));
    }

    // ========== 精准溯源查询 ==========

    /// <summary>溯源查询入口页</summary>
    [HttpGet]
    public IActionResult Traceability() => View();

    /// <summary>按订单 ID 查询完整溯源链路</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            ViewBag.Error = "请输入订单 ID";
            return View("Traceability");
        }
        var result = await _logistics.GetTraceabilityByOrderAsync(orderId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            return View("Traceability");
        }
        ViewBag.TraceResult = result.Data;
        ViewBag.QueryType = "order";
        ViewBag.QueryKey = orderId;
        return View("Traceability");
    }

    /// <summary>按发货单 ID 查询单张发货单的批次明细</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByDelivery(string deliveryId)
    {
        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            ViewBag.Error = "请输入发货单 ID";
            return View("Traceability");
        }
        var result = await _logistics.GetTraceabilityByDeliveryAsync(deliveryId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            return View("Traceability");
        }
        ViewBag.TraceResult = new List<DeliveryTraceDto> { result.Data! };
        ViewBag.QueryType = "delivery";
        ViewBag.QueryKey = deliveryId;
        return View("Traceability");
    }

    /// <summary>反向溯源：按批次 ID 查去向</summary>
    [HttpGet]
    public async Task<IActionResult> TraceByBatch(string batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            ViewBag.Error = "请输入批次 ID";
            return View("Traceability");
        }
        var result = await _logistics.GetBatchTraceAsync(batchId);
        if (!result.IsSuccess)
        {
            ViewBag.Error = result.Message;
            return View("Traceability");
        }
        ViewBag.BatchTrace = result.Data;
        ViewBag.QueryType = "batch";
        ViewBag.QueryKey = batchId;
        return View("Traceability");
    }
}
