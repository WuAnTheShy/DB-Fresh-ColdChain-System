using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

// 订单 Controller，负责查询、创建和状态流转入口。
[ServiceFilter(typeof(GroupBAdminSessionAuthorizationFilter))]
public sealed class OrderController : Controller
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

    [HttpGet]
    [GroupBPermission(GroupBPermissions.OrdersRead)]
    public async Task<IActionResult> Index(OrderQueryRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ErrorMessage"] = "订单查询条件无效，请检查后重试";
            return View(new OrderListViewModel { Query = request });
        }

        try
        {
            return View(await _orderService.GetOrdersAsync(request));
        }
        catch (OrderBusinessException exception)
        {
            ViewData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "查询订单列表失败");
            ViewData["ErrorMessage"] = "系统暂时无法查询订单，请稍后重试";
        }

        return View(new OrderListViewModel { Query = request });
    }

    [HttpGet]
    [GroupBPermission(GroupBPermissions.OrdersManage)]
    public IActionResult Create()
    {
        return View(new CreateOrderRequest());
    }

    [HttpPost]
    [GroupBPermission(GroupBPermissions.OrdersManage)]
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

    [HttpGet]
    [GroupBPermission(GroupBPermissions.OrdersRead)]
    public async Task<IActionResult> Detail(string id)
    {
        if (!GroupBIds.IsValid(id))
            return BadRequest();

        try
        {
            var model = await _orderService.GetOrderDetailAsync(id);
            return model == null ? NotFound() : View(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "查询订单 {OrderId} 详情失败", id);
            ViewData["ErrorMessage"] = "系统暂时无法查询订单详情，请稍后重试";
            return View(new OrderDetailViewModel { OrderId = id });
        }
    }

    [HttpPost]
    [GroupBPermission(GroupBPermissions.OrdersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(
        string id,
        OrderStatus targetStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderService.TransitionOrderAsync(
                id,
                targetStatus,
                cancellationToken);
            TempData["SuccessMessage"] =
                $"订单已更新为“{OrderStatusNames.GetName(targetStatus)}”";
        }
        catch (OrderBusinessException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "订单 {OrderId} 状态流转失败",
                id);
            TempData["ErrorMessage"] = "系统暂时无法更新订单状态，请稍后重试";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [GroupBPermission(GroupBPermissions.OrdersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderService.CancelOrderAsync(id, cancellationToken);
            TempData["SuccessMessage"] = "订单已取消，库存与本组营销资产已归还";
        }
        catch (OrderBusinessException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "取消订单 {OrderId} 失败", id);
            TempData["ErrorMessage"] = "系统暂时无法取消订单，请稍后重试";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    private static void EnsureAtLeastOneItem(CreateOrderRequest request)
    {
        request.Items ??= [];
        if (request.Items.Count == 0)
            request.Items.Add(new CreateOrderItemRequest());
    }
}
