using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

/// <summary>B 组消费者端真实商品目录，只读且无需登录。</summary>
[ApiController]
[Route("api/consumer-catalog")]
public sealed class ConsumerCatalogApiController(
    IConsumerCatalogService catalogService) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        return Ok(await catalogService.GetCatalogAsync(cancellationToken));
    }
}
