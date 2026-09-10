namespace FreshColdChain.Services;

// 在密码重置后阻止旧消费者 Session 继续访问受保护 API。
public sealed class CustomerSessionValidationMiddleware(RequestDelegate next)
{
    private const string CustomerIdSessionKey = "CustomerId";
    private const string AuthenticationVersionSessionKey = "CustomerAuthenticationVersion";

    private static readonly PathString[] AnonymousAuthPaths =
    [
        new("/api/auth/customer/login"),
        new("/api/auth/customer/register"),
        new("/api/auth/customer/password-reset/code"),
        new("/api/auth/customer/password-reset"),
        new("/api/auth/customer/logout")
    ];

    public async Task InvokeAsync(
        HttpContext context,
        CustomerAuthenticationStateService authenticationState)
    {
        if (context.Request.Path.StartsWithSegments("/api") &&
            !AnonymousAuthPaths.Contains(context.Request.Path) &&
            context.Session.GetString(CustomerIdSessionKey) is { Length: > 0 } customerId)
        {
            var signedInVersion = context.Session.GetInt32(AuthenticationVersionSessionKey) ?? 0;
            if (signedInVersion != authenticationState.GetAuthenticationVersion(customerId))
            {
                context.Session.Clear();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "密码已重置，请重新登录",
                    traceId = context.TraceIdentifier
                });
                return;
            }
        }

        await next(context);
    }
}
