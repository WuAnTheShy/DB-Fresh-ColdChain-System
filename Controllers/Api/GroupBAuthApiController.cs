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
<<<<<<< HEAD
        [FromServices] ICustomerService customerService,
        [FromServices] CustomerAuthenticationStateService authenticationState)
=======
        [FromServices] ICustomerService customerService)
>>>>>>> origin/dev-groupC
    {
        var customerId = await customerService.CreateCustomerAsync(request);
        var result = new GroupBCustomerLoginResult
        {
            CustomerId = customerId,
            CustomerName = request.CustomerName,
<<<<<<< HEAD
            Phone = request.Phone
        };
        SignIn(result, authenticationState.GetAuthenticationVersion(customerId));
=======
            Phone = request.Phone,
            Avatar = request.Avatar
        };
        SignIn(result);
>>>>>>> origin/dev-groupC

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        GroupBCustomerLoginRequest request,
<<<<<<< HEAD
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

=======
        [FromServices] ICustomerService customerService)
    {
        var result = await customerService.LoginAsync(request);
        SignIn(result);
        return Ok(result);
    }

>>>>>>> origin/dev-groupC
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
<<<<<<< HEAD
            Phone = HttpContext.Session.GetString(CustomerPhoneSessionKey) ?? string.Empty
=======
            Phone = HttpContext.Session.GetString(CustomerPhoneSessionKey) ?? string.Empty,
            Avatar = HttpContext.Session.GetString(CustomerAvatarSessionKey)
>>>>>>> origin/dev-groupC
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
<<<<<<< HEAD
        HttpContext.Session.Clear();
        return NoContent();
    }

    private void SignIn(GroupBCustomerLoginResult customer, int authenticationVersion)
=======
        HttpContext.Session.Remove(CustomerIdSessionKey);
        HttpContext.Session.Remove(CustomerNameSessionKey);
        HttpContext.Session.Remove(CustomerPhoneSessionKey);
        HttpContext.Session.Remove(CustomerAvatarSessionKey);
        return NoContent();
    }

    private void SignIn(GroupBCustomerLoginResult customer)
>>>>>>> origin/dev-groupC
    {
        HttpContext.Session.SetString(CustomerIdSessionKey, customer.CustomerId);
        HttpContext.Session.SetString(CustomerNameSessionKey, customer.CustomerName);
        HttpContext.Session.SetString(CustomerPhoneSessionKey, customer.Phone);
<<<<<<< HEAD
        HttpContext.Session.SetInt32(
            CustomerAuthenticationVersionSessionKey,
            authenticationVersion);
=======
        if (!string.IsNullOrWhiteSpace(customer.Avatar))
        {
            HttpContext.Session.SetString(CustomerAvatarSessionKey, customer.Avatar);
        }
>>>>>>> origin/dev-groupC
    }
}
