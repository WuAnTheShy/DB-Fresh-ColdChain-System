using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

public class OrdersController : Controller
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int pageIndex = 1, int pageSize = 10, int? status = null)
    {
        var result = await _orderService.GetOrdersAsync(pageIndex, pageSize, status);
        ViewBag.CurrentStatus = status;
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _orderService.GetOrderDetailAsync(id);
        if (!result.IsSuccess)
            return NotFound(result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateOrderDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOrderDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _orderService.CreateOrderAsync(dto);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Message);
            return View(dto);
        }

        TempData["Success"] = "订单创建成功";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, int newStatus)
    {
        var result = await _orderService.UpdateOrderStatusAsync(id, newStatus);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _orderService.CancelOrderAsync(id);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ByLeader(int leaderId)
    {
        var result = await _orderService.GetOrdersByLeaderAsync(leaderId);
        ViewBag.LeaderId = leaderId;
        return View(result.Data);
    }
}
