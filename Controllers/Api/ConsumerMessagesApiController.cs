using FreshColdChain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

[ApiController]
[Route("api/customers/{customerId}/messages")]
public sealed class ConsumerMessagesApiController(IConsumerMessageService service) : GroupBApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMessages(string customerId, [FromQuery] int take = 100)
    {
        var error = AuthorizeCustomer(customerId);
        return error ?? Ok(new { messages = await service.GetMessagesAsync(customerId, take) });
    }
}
