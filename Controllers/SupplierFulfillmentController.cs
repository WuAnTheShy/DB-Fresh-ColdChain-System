using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers;

/// <summary>B 组供应商履约入口；所有数据范围均由当前供应商会话决定。</summary>
[ServiceFilter(typeof(GroupBSupplierSessionAuthorizationFilter))]
public sealed class SupplierFulfillmentController(
    ISupplierFulfillmentService fulfillmentService,
    ILogger<SupplierFulfillmentController> logger) : Controller
{
    private string SupplierId =>
        HttpContext.Session.GetString("SupplierId")!;

    [HttpGet]
    public async Task<IActionResult> Index(
        SupplierFulfillmentQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return View(await fulfillmentService.GetOrdersAsync(
                SupplierId,
                query,
                cancellationToken));
        }
        catch (OrderBusinessException exception)
        {
            ViewData["Error"] = exception.Message;
            return View(new SupplierFulfillmentListViewModel
            {
                SupplierId = SupplierId,
                Query = query
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await fulfillmentService.GetOrderAsync(
                SupplierId,
                id,
                cancellationToken);
            return model == null ? NotFound() : View(model);
        }
        catch (OrderBusinessException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ship(
        string id,
        SupplierShipmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "发货信息不完整，请检查后重试";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var trustedCommand = new SupplierShipmentCommand
        {
            SupplierId = SupplierId,
            CarrierCode = command.CarrierCode,
            CarrierName = command.CarrierName,
            TrackingNo = command.TrackingNo,
            PackageTemperature = command.PackageTemperature,
            EstimatedArrivalAt = command.EstimatedArrivalAt,
            Remark = command.Remark
        };

        try
        {
            var result = await fulfillmentService.ShipAsync(
                SupplierId,
                id,
                trustedCommand,
                cancellationToken);
            TempData["Success"] = $"订单已发货，运单号：{result.TrackingNo}";
        }
        catch (OrderBusinessException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "供应商 {SupplierId} 发货订单 {OrderId} 失败", SupplierId, id);
            TempData["Error"] = "系统暂时无法完成发货，请稍后重试";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTrackingEvent(
        string id,
        LogisticsTrackingEventCommand command,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "物流事件信息不完整，请检查后重试";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var trustedCommand = new LogisticsTrackingEventCommand
        {
            EventId = command.EventId,
            OrderId = id,
            SupplierId = SupplierId,
            StatusCode = command.StatusCode,
            Location = command.Location,
            Description = command.Description,
            OccurredAt = command.OccurredAt,
            TemperatureCelsius = command.TemperatureCelsius
        };

        try
        {
            var result = await fulfillmentService.AppendTrackingEventAsync(
                SupplierId,
                id,
                trustedCommand,
                cancellationToken);
            TempData["Success"] = $"物流状态已更新为：{result.StatusName}";
        }
        catch (OrderBusinessException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "供应商 {SupplierId} 更新订单 {OrderId} 轨迹失败", SupplierId, id);
            TempData["Error"] = "系统暂时无法更新物流轨迹，请稍后重试";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }
}
