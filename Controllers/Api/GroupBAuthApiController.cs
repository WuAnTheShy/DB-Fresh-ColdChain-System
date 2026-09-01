using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Services;
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
        [FromServices] ICustomerService customerService,
        [FromServices] CustomerAuthenticationStateService authenticationState)
    {
        var customerId = await customerService.CreateCustomerAsync(request);
        var result = new GroupBCustomerLoginResult
        {
            CustomerId = customerId,
            CustomerName = request.CustomerName,
            Phone = request.Phone,
            Avatar = request.Avatar
        };
        SignIn(result, authenticationState.GetAuthenticationVersion(customerId));

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        GroupBCustomerLoginRequest request,
        [FromServices] ICustomerService customerService,
        [FromServices] CustomerAuthenticationStateService authenticationState)
    {
        var result = await customerService.LoginAsync(request);
        SignIn(result, authenticationState.GetAuthenticationVersion(result.CustomerId));
        return Ok(result);
    }

    [HttpPost("password-reset/code")]
    public async Task<IActionResult> SendPasswordResetCode(
        GroupBCustomerPasswordResetCodeRequest request,
        [FromServices] ICustomerService customerService)
    {
        return Ok(await customerService.SendPasswordResetCodeAsync(request));
    }

    [HttpPost("password-reset")]
    public async Task<IActionResult> ResetPassword(
        GroupBCustomerPasswordResetRequest request,
        [FromServices] ICustomerService customerService)
    {
        await customerService.ResetPasswordAsync(request);
        HttpContext.Session.Clear();
        return NoContent();
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
        HttpContext.Session.Clear();
        return NoContent();
    }

    private void SignIn(GroupBCustomerLoginResult customer, int authenticationVersion)
    {
        HttpContext.Session.SetString(CustomerIdSessionKey, customer.CustomerId);
        HttpContext.Session.SetString(CustomerNameSessionKey, customer.CustomerName);
        HttpContext.Session.SetString(CustomerPhoneSessionKey, customer.Phone);
        HttpContext.Session.SetInt32(
            CustomerAuthenticationVersionSessionKey,
            authenticationVersion);
        if (!string.IsNullOrWhiteSpace(customer.Avatar))
        {
            HttpContext.Session.SetString(CustomerAvatarSessionKey, customer.Avatar);
        }
    }
}
