using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

/// <summary>
/// 订单 Controller - 接收前端请求，调用 Service，返回页面
/// </summary>
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ICouponService _couponService;

    // 构造函数注入：框架自动把 Service 实例传进来
    public OrderController(IOrderService orderService, ICouponService couponService)
    {
        _orderService = orderService;
        _couponService = couponService;
    }

    /// <summary>
    /// 下单页面 (GET)
    /// 浏览器访问: /Order/Create
    /// </summary>
    public IActionResult Create()
    {
        return View();
    }

    /// <summary>
    /// 提交订单 (POST)
    /// 表单提交到这里
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(BizOrder order)
    {
        // 示例：创建一笔订单的明细
        var details = new List<BizOrderDetail>
        {
            new() { ProductId = 1, ProductName = "车厘子", Quantity = 2, UnitPrice = 50, SubTotal = 100, SupplierId = 1 },
            new() { ProductId = 2, ProductName = "三文鱼", Quantity = 1, UnitPrice = 80, SubTotal = 80, SupplierId = 2 }
        };

        order.OrderNo = "ORD" + DateTime.Now.ToString("yyyyMMddHHmmss");
        order.FinalAmount = details.Sum(d => d.SubTotal);
        order.TotalAmount = order.FinalAmount;
        order.OrderStatus = 1; // 已支付
        order.CreatedAt = DateTime.Now;

        try
        {
            var orderId = await _orderService.CreateOrderAsync(order, details);
            ViewBag.Message = $"下单成功！订单ID: {orderId}";
        }
        catch (Exception ex)
        {
            ViewBag.Message = $"下单失败：{ex.Message}";
        }

        return View();
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
}
