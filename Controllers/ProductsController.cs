//负责处理与生鲜产品和商品库存相关的页面跳转与请求交互

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Filters;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

[RequireSupplier]
public class ProductsController : Controller
{
    private readonly IProductInventoryService _service;

    public ProductsController(IProductInventoryService service) => _service = service;

    // ========== 产品 ==========

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10, string? keyword = null)
    {
        var r = await _service.GetProductsAsync(pageIndex, pageSize, keyword);
        ViewBag.Keyword = keyword;
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, string? supplierId)
    {
        var r = await _service.GetProductByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);

        // 供应商图文区块：下拉选择供应商后显示其简介与图片
        var media = await _service.GetSupplierProductMediaAsync(id, supplierId);
        ViewBag.Media = media.Data ?? new ProductSupplierMediaDto();
        return View(r.Data);
    }

    [HttpGet]
    [RequireAdmin]
    public IActionResult Create() => View(new CreateProductDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Create(CreateProductDto dto)
    {
        var r = await _service.CreateProductAsync(dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<IActionResult> Edit(string id)
    {
        var r = await _service.GetProductByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        var p = r.Data!;
        return View(new UpdateProductDto
        {
            ProductName = p.ProductName, Unit = p.Unit,
            WeightKG = p.WeightKG, VolumeLitre = p.VolumeLitre,
            ExpiryHours = p.ExpiryHours, StorageReq = p.StorageReq,
            Description = p.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Edit(string id, UpdateProductDto dto)
    {
        var r = await _service.UpdateProductAsync(id, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireAdmin]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _service.DeleteProductAsync(id);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // ========== 库存 ==========

    [HttpGet]
    public async Task<IActionResult> Inventory(string productId)
    {
        // 自动标记已过期批次
        await _service.MarkExpiredBatchesAsync();
        var r = await _service.GetInventoryAsync(productId);
        if (!r.IsSuccess) return NotFound(r.Message);
        var batches = await _service.GetBatchesAsync(productId);
        ViewBag.Batches = batches.Data;
        var options = await _service.GetStockInSupplierOptionsAsync(productId);
        ViewBag.SupplierOptions = options.Data ?? new List<SupplierQuoteOptionDto>();
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(int threshold = 10)
    {
        var r = await _service.GetLowStockProductsAsync(threshold);
        ViewBag.Threshold = threshold;
        return View(r.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockIn(string productId, int quantity, string? supplierId, string? batchNo, DateTime? productionDate)
    {
        var r = await _service.StockInAsync(
            new UpdateInventoryDto { ProductID = productId, Quantity = quantity },
            supplierId, batchNo, productionDate);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockOut(string productId, int quantity)
    {
        var r = await _service.StockOutAsync(new UpdateInventoryDto { ProductID = productId, Quantity = quantity });
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBatch(CreateStockBatchDto dto)
    {
        var r = await _service.AddBatchAsync(dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Inventory), new { productId = dto.ProductID });
    }
}
