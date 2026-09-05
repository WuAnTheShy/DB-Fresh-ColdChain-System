using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Filters;

/// <summary>
/// 要求「供应商」登录（普通供应商或供应商管理员），否则跳转供应商登录页。
/// 数据归属过滤在服务层按 SupplierID 强制。
/// </summary>
public class RequireSupplierAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!SupplierSession.IsSupplier(context.HttpContext.Session))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { role = "供应商" });
        }
    }
}
