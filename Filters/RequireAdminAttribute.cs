using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Filters;

/// <summary>
/// 要求「供应商管理员」登录（固定账号 admin，Session["IsSupplierAdmin"]="true"），
/// 否则跳转供应商登录页。用于保护 A 组平台级操作：增加物品、修改运费模板、供应商管理。
/// 注意：这不是 B 组/C 组的平台管理员（Session["AdminName"]）。
/// </summary>
public class RequireAdminAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!SupplierSession.IsSupplierAdmin(context.HttpContext.Session))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { role = "供应商" });
        }
    }
}
