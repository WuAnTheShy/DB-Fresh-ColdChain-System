using System.Reflection;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Services;

/// <summary>B 组仅向 C 组请求授权，不把管理员名称当作权限。</summary>
public sealed class GroupBAdminSessionAuthorizationFilter(IGroupCAuthorizationService authorization,
    ILogger<GroupBAdminSessionAuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        GroupBAuthorizationFilterHelper.AuthorizeAsync(context, authorization, logger, "AdminId", "管理员", false);
}

/// <summary>供应商操作权限与账号状态由 C 组验证，履约服务继续检查具体订单归属。</summary>
public sealed class GroupBSupplierSessionAuthorizationFilter(IGroupCAuthorizationService authorization,
    ILogger<GroupBSupplierSessionAuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        GroupBAuthorizationFilterHelper.AuthorizeAsync(context, authorization, logger, "SupplierId", "供应商", true);
}

internal static class GroupBAuthorizationFilterHelper
{
    public static async Task AuthorizeAsync(AuthorizationFilterContext context, IGroupCAuthorizationService authorization,
        ILogger logger, string sessionKey, string loginRole, bool supplier)
    {
        var subjectId = context.HttpContext.Session.GetString(sessionKey);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { role = loginRole });
            return;
        }
        var permission = (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo
            .GetCustomAttribute<GroupBPermissionAttribute>()?.Code;
        if (permission == null || (supplier
            ? permission is not (GroupBPermissions.FulfillmentRead or GroupBPermissions.FulfillmentWrite)
            : permission is not (GroupBPermissions.OrdersRead or GroupBPermissions.OrdersManage)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }
        try
        {
            var result = await authorization.AuthorizeAsync(subjectId, permission,
                supplier ? subjectId : null, context.HttpContext.RequestAborted);
            if (!result.IsAllowed || result.SubjectId != subjectId)
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "C 组操作授权查询失败，权限代码 {PermissionCode}", permission);
            // 授权故障不降级放行，也不误报为账号已被撤权。
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
