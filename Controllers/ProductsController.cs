using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

public class ProductsController : Controller
{
    private readonly IProductInventoryService _service;

    public ProductsController(IProductInventoryService service)
    {
        _service = service;
    }

    // ========== 产品管理 ==========

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10, string? keyword = null)
    {
        var result = await _service.GetProductsAsync(pageIndex, pageSize, keyword);
        ViewBag.Keyword = keyword;
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _service.GetProductByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateProductDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _service.CreateProductAsync(dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _service.GetProductByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);

        var dto = new UpdateProductDto
        {
            Name = result.Data!.Name,
            Category = result.Data.Category,
            Unit = result.Data.Unit,
            Price = result.Data.Price,
            ImageUrl = result.Data.ImageUrl,
            Status = result.Data.Status
        };
        ViewBag.ProductId = id;
        ViewBag.ProductName = result.Data.Name;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateProductDto dto)
    {
        var result = await _service.UpdateProductAsync(id, dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            ViewBag.ProductId = id;
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteProductAsync(id);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // ========== 库存管理 ==========

    [HttpGet]
    public async Task<IActionResult> Inventory(int productId)
    {
        var result = await _service.GetInventoryAsync(productId);
        if (!result.IsSuccess)
            return NotFound(result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(int threshold = 10)
    {
        var result = await _service.GetLowStockProductsAsync(threshold);
        ViewBag.Threshold = threshold;
        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockIn(UpdateInventoryDto dto)
    {
        var result = await _service.StockInAsync(dto);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Inventory), new { productId = dto.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockOut(UpdateInventoryDto dto)
    {
        var result = await _service.StockOutAsync(dto);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Inventory), new { productId = dto.ProductId });
    }
}
