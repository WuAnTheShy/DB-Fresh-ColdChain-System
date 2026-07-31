using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

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
    public async Task<IActionResult> Details(string id)
    {
        var r = await _service.GetProductByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        return View(r.Data);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateProductDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductDto dto)
    {
        var r = await _service.CreateProductAsync(dto);
        TempData["Success"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
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
            DefaultPrice = p.DefaultPrice, Status = p.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UpdateProductDto dto)
    {
        var r = await _service.UpdateProductAsync(id, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
        var r = await _service.GetInventoryAsync(productId);
        if (!r.IsSuccess) return NotFound(r.Message);
        var batches = await _service.GetBatchesAsync(productId);
        ViewBag.Batches = batches.Data;
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
    public async Task<IActionResult> StockIn(string productId, int quantity, string? batchNo)
    {
        var r = await _service.StockInAsync(new UpdateInventoryDto { ProductID = productId, Quantity = quantity }, batchNo);
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
