using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Services;

/// <summary>
/// B 组订单管理页的最小权限边界。
/// 当前统一登录由 C 组建立管理员会话；后续接入 RBAC 时只需替换本过滤器，
/// 订单 Controller 不直接依赖 C 组用户表或权限表。
/// </summary>
public sealed class GroupBAdminSessionAuthorizationFilter : IAuthorizationFilter
{
    private const string AdminNameSessionKey = "AdminName";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!string.IsNullOrWhiteSpace(
                context.HttpContext.Session.GetString(AdminNameSessionKey)))
        {
            return;
        }

        context.Result = new RedirectToActionResult(
            "Login",
            "Account",
            new { role = "管理员" });
    }
}
