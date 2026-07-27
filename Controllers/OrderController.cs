using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

/// <summary>
/// 订单 Controller - 接收前端请求，调用 Service，返回页面
/// </summary>
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderService orderService,
        ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// 下单页面 (GET)
    /// 浏览器访问: /Order/Create
    /// </summary>
    public IActionResult Create()
    {
        return View(new CreateOrderRequest());
    }

    /// <summary>
    /// 提交订单 (POST)
    /// 表单提交到这里
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        EnsureAtLeastOneItem(request);
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            ViewData["OrderResult"] = await _orderService.CreateOrderAsync(
                request,
                cancellationToken);
        }
        catch (OrderBusinessException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "创建订单失败");
            ModelState.AddModelError(
                string.Empty,
                "系统暂时无法创建订单，请稍后重试");
        }

        return View(request);
    }

    /// <summary>
    /// 订单详情页面
    /// 浏览器访问: /Order/Detail/1
    /// </summary>
    public IActionResult Detail(int id)
    {
        ViewBag.OrderId = id;
        return View();
    }

    private static void EnsureAtLeastOneItem(CreateOrderRequest request)
    {
        request.Items ??= [];
        if (request.Items.Count == 0)
            request.Items.Add(new CreateOrderItemRequest());
    }
}
