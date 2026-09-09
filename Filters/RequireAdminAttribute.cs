using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Filters;

/// <summary>
/// 要求「平台商品管理员」登录（可查看/操作全部供应商数据）：
/// - C 组商品管理员（Session["AdminKind"]="PRODUCT"，在管理后台外壳下复用 A 组平台级页面）；
/// - 兼容旧的 A 组供应商管理员会话（固定账号 admin 已随 20260906 迁移删除，仅遗留会话可命中）。
/// 否则跳转对应登录入口。用于保护平台级操作：增加物品、修改运费模板、供应商管理、全部货物等。
/// </summary>
public class RequireAdminAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (SupplierSession.IsSupplierAdmin(session) || AdminSession.IsProductAdmin(session))
        {
            return;
        }

        // C 组其它管理员已登录但无此权限 → 回到各自工作台首页
        if (AdminSession.IsLoggedIn(session))
        {
            context.Result = new RedirectToActionResult("Dashboard", "Admins", null);
            return;
        }

        context.Result = new RedirectToActionResult("Login", "Account", new { role = "供应商" });
    }
}
