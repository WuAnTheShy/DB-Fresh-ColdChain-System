using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

public class SuppliersController : Controller
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10)
    {
        var result = await _service.GetSuppliersAsync(pageIndex, pageSize);
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _service.GetSupplierByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateSupplierDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _service.CreateSupplierAsync(dto);
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
        var result = await _service.GetSupplierByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);

        return View(new CreateSupplierDto
        {
            Name = result.Data!.Name,
            ContactPerson = result.Data.ContactPerson,
            Phone = result.Data.Phone,
            Address = result.Data.Address,
            Remark = result.Data.Remark
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreateSupplierDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _service.UpdateSupplierAsync(id, dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteSupplierAsync(id);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
