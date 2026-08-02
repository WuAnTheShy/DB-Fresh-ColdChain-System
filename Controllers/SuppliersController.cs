//负责管理与供应商（合作商）相关的交互请求

using Microsoft.AspNetCore.Mvc;
using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Controllers;

public class SuppliersController : Controller
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10)
    {
        var r = await _service.GetSuppliersAsync(pageIndex, pageSize);
        return View(r.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var r = await _service.GetSupplierByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        return View(r.Data);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateSupplierDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierDto dto)
    {
        var r = await _service.CreateSupplierAsync(dto);
        TempData["Success"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var r = await _service.GetSupplierByIdAsync(id);
        if (!r.IsSuccess) return NotFound(r.Message);
        return View(new CreateSupplierDto
        {
            SupplierName = r.Data!.SupplierName, LicenseNo = r.Data.LicenseNo,
            ExpiryDate = r.Data.ExpiryDate, CreditLevel = r.Data.CreditLevel,
            ContactPhone = r.Data.ContactPhone, LoginAccount = r.Data.LoginAccount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, CreateSupplierDto dto)
    {
        var r = await _service.UpdateSupplierAsync(id, dto);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var r = await _service.DeleteSupplierAsync(id);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
