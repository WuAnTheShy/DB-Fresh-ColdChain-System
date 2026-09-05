using System.Security.Cryptography;
using System.Text;
using FreshColdChain.Models;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Controllers.Api;

/// <summary>仅用于开发演示的独立物流商接入，不使用消费者或供应商浏览器会话。</summary>
[ApiController]
[Route("api/demo-carrier/shipments")]
[ServiceFilter(typeof(DemoCarrierAuthorizationFilter))]
public sealed class DemoCarrierApiController(GroupADemoCarrierService carrier, ILogger<DemoCarrierApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? keyword, CancellationToken token)
    {
        if (keyword?.Length > 100) return BadRequest(new { message = "查询关键词过长" });
        return await ExecuteAsync(async () => Ok(await carrier.SearchAsync(keyword, token)));
    }

    [HttpGet("{deliveryId}")]
    public Task<IActionResult> Get(string deliveryId, CancellationToken token) => ExecuteAsync(async () =>
        await carrier.GetAsync(deliveryId, token) is { } result ? Ok(result) : NotFound(new { message = "运单不存在或不在演示授权范围" }));

    [HttpPost("{deliveryId}/events")]
    public Task<IActionResult> Append(string deliveryId, CarrierEventCommand command, CancellationToken token) => ExecuteAsync(async () =>
        await carrier.AppendAsync(deliveryId, command, token) is { } result ? Ok(result) : NotFound(new { message = "运单不存在或不在演示授权范围" }));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (OrderBusinessException exception) { return Conflict(new { message = exception.Message }); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "物流商演示接口失败，TraceId={TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(503, new { message = "物流服务暂时不可用，请确认数据库迁移及连接状态", traceId = HttpContext.TraceIdentifier });
        }
    }
}

public sealed class DemoCarrierAuthorizationFilter(IOptions<DemoCarrierOptions> options,
    IOptions<GroupALogisticsOptions> logisticsOptions, IHostEnvironment environment) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var settings = options.Value;
        if (!environment.IsDevelopment() || !settings.Enabled || logisticsOptions.Value.Provider != "Oracle")
            context.Result = new NotFoundResult();
        else if (settings.ApiKey.Length < 32 || settings.SupplierIds.Length == 0 ||
            !context.HttpContext.Request.Headers.TryGetValue("X-Carrier-Key", out var header) || header.Count != 1 ||
            header.ToString().Length > 256 || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(header.ToString())), SHA256.HashData(Encoding.UTF8.GetBytes(settings.ApiKey))))
            context.Result = new UnauthorizedObjectResult(new { message = "物流商凭据无效" });
        return Task.CompletedTask;
    }
}
