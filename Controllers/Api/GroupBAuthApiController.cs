using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

/// <summary>B 组消费者统一注册与登录入口。</summary>
[ApiController]
[Route("api/auth/customer")]
public sealed class GroupBAuthApiController(
    ICustomerService customerService) : GroupBApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(CustomerCreateRequest request)
    {
        var customerId = await customerService.CreateCustomerAsync(request);
        return StatusCode(
            StatusCodes.Status201Created,
            new { customerId });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(GroupBCustomerLoginRequest request)
    {
        return Ok(await customerService.LoginAsync(request));
    }
}
