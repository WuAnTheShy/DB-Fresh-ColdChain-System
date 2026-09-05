using CarrierSimulator.Models;
using CarrierSimulator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CarrierSimulator.Controllers;

public sealed class ShipmentsController(CarrierClient client, IOptions<CarrierClientOptions> options,
    ILogger<ShipmentsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? id, string? keyword, CancellationToken token) =>
        View(await LoadAsync(id, keyword, token));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(EventInput input, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await client.AppendAsync(input, token);
                TempData["Success"] = "物流更新已保存到 Oracle，请在商城刷新查看";
                return RedirectToAction(nameof(Index), new { id = input.DeliveryId });
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
            { ModelState.AddModelError("", Message(exception)); }
        }
        var page = await LoadAsync(input.DeliveryId, null, token);
        // 保留事件编号，超时或重复提交时由服务端核验原始载荷。
        page.Input = input;
        return View("Index", page);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Handoff(HandoffInput input, CancellationToken token)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "接单参数不完整，请刷新后重试";
            return RedirectToAction(nameof(Index), new { keyword = input.Keyword });
        }
        try
        {
            var shipment = await client.HandoffAsync(input, token);
            TempData["Success"] = $"已生成真实运单：{shipment.TrackingNo ?? shipment.DeliveryId}";
            return RedirectToAction(nameof(Index), new { id = shipment.DeliveryId, keyword = input.Keyword });
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            TempData["Error"] = Message(exception);
            return RedirectToAction(nameof(Index), new { keyword = input.Keyword });
        }
    }

    private async Task<ShipmentsPage> LoadAsync(string? id, string? keyword, CancellationToken token)
    {
        var page = new ShipmentsPage { Keyword = keyword, ShopUrl = options.Value.BaseUrl.TrimEnd('/') + "/app/" };
        try
        {
            page.Shipments = await client.SearchAsync(keyword, token);
            id ??= page.Shipments.FirstOrDefault(item => item.HasShipment)?.DeliveryId;
            if (id != null)
            {
                page.Selected = await client.GetAsync(id, token);
                page.Input.DeliveryId = id;
                page.Input.StatusCode = page.Selected.StatusCode switch
                {
                    "SHIPPED" or "EXCEPTION" => "IN_TRANSIT", "IN_TRANSIT" => "OUT_FOR_DELIVERY",
                    "OUT_FOR_DELIVERY" => "DELIVERED", "RETURNING" => "RETURNED", _ => page.Selected.StatusCode
                };
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        { page.Error = Message(exception); }
        return page;
    }
    private string Message(Exception exception)
    {
        if (exception is CarrierRequestException) return exception.Message;
        logger.LogWarning("物流商演示请求失败，类型={ExceptionType}", exception.GetType().Name);
        return "无法连接物流接口或响应超时，请确认商城已启动后重试；重复提交请保留事件编号";
    }
}
