using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;

namespace FreshColdChain.Controllers.Api;

/// <summary>B 组消费者统一注册与登录入口。</summary>
[ApiController]
[Route("api/auth/customer")]
public sealed class GroupBAuthApiController : GroupBApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        CustomerCreateRequest request,
        [FromServices] ICustomerService customerService)
    {
        var customerId = await customerService.CreateCustomerAsync(request);
        var result = new GroupBCustomerLoginResult
        {
            CustomerId = customerId,
            CustomerName = request.CustomerName,
            Phone = request.Phone,
            Avatar = request.Avatar
        };
        SignIn(result);

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        GroupBCustomerLoginRequest request,
        [FromServices] ICustomerService customerService)
    {
        var result = await customerService.LoginAsync(request);
        SignIn(result);
        return Ok(result);
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var customerId = HttpContext.Session.GetString(CustomerIdSessionKey);
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Unauthorized(new
            {
                message = "请先登录消费者账号",
                traceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new GroupBCustomerLoginResult
        {
            CustomerId = customerId,
            CustomerName = HttpContext.Session.GetString(CustomerNameSessionKey) ?? string.Empty,
            Phone = HttpContext.Session.GetString(CustomerPhoneSessionKey) ?? string.Empty,
            Avatar = HttpContext.Session.GetString(CustomerAvatarSessionKey)
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(CustomerIdSessionKey);
        HttpContext.Session.Remove(CustomerNameSessionKey);
        HttpContext.Session.Remove(CustomerPhoneSessionKey);
        HttpContext.Session.Remove(CustomerAvatarSessionKey);
        return NoContent();
    }

    private void SignIn(GroupBCustomerLoginResult customer)
    {
        HttpContext.Session.SetString(CustomerIdSessionKey, customer.CustomerId);
        HttpContext.Session.SetString(CustomerNameSessionKey, customer.CustomerName);
        HttpContext.Session.SetString(CustomerPhoneSessionKey, customer.Phone);
        if (!string.IsNullOrWhiteSpace(customer.Avatar))
        {
            HttpContext.Session.SetString(CustomerAvatarSessionKey, customer.Avatar);
        }
    }
}
