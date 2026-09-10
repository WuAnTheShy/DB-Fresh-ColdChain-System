using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Filters;

// 要求「供应商」登录（普通供应商或遗留供应商管理员会话），或「C 组商品管理员」，
// 否则跳转对应登录入口。数据归属过滤在服务层按 SupplierID 强制。
public class RequireSupplierAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (SupplierSession.IsSupplier(session) || AdminSession.IsProductAdmin(session))
        {
            return;
        }

        if (AdminSession.IsLoggedIn(session))
        {
            context.Result = new RedirectToActionResult("Dashboard", "Admins", null);
            return;
        }

        context.Result = new RedirectToActionResult("Login", "Account", new { role = "供应商" });
    }
}
