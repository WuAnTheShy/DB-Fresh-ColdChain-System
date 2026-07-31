using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

public class GroupLeadersController : Controller
{
    private readonly IGroupLeaderService _service;

    public GroupLeadersController(IGroupLeaderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10)
    {
        var result = await _service.GetLeadersAsync(pageIndex, pageSize);
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _service.GetLeaderByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateGroupLeaderDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateGroupLeaderDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _service.CreateLeaderAsync(dto);
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
        var result = await _service.GetLeaderByIdAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);

        return View(new CreateGroupLeaderDto
        {
            Name = result.Data!.Name,
            Phone = result.Data.Phone,
            CommunityName = result.Data.CommunityName,
            Address = result.Data.Address
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreateGroupLeaderDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _service.UpdateLeaderAsync(id, dto);
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
        var result = await _service.DeleteLeaderAsync(id);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int id)
    {
        var result = await _service.EnableLeaderAsync(id);
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {
        var result = await _service.DisableLeaderAsync(id);
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
