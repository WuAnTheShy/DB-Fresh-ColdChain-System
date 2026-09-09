using Microsoft.AspNetCore.Http;

namespace FreshColdChain.Filters;

/// <summary>
/// C 组管理员登录态助手。
/// 管理员身份被细分为四类（SYS_USERS.ADMIN_KIND）：
/// ACCOUNT 账号管理员 / FINANCE 财务管理员 / LOG 日志管理员 / PRODUCT 商品管理员。
/// 登录成功后写入 Session["AdminId"] / Session["AdminName"] / Session["AdminKind"]。
/// </summary>
public static class AdminSession
{
    public const string KindKey = "AdminKind";

    public const string AccountKind = "ACCOUNT";   // 账号管理员
    public const string FinanceKind = "FINANCE";   // 财务管理员
    public const string LogKind = "LOG";           // 日志管理员
    public const string ProductKind = "PRODUCT";   // 商品管理员

    /// <summary>四种管理员种类（Code 入库，Name 展示）。</summary>
    public static readonly (string Code, string Name)[] Kinds =
    {
        (AccountKind, "账号管理员"),
        (FinanceKind, "财务管理员"),
        (LogKind, "日志管理员"),
        (ProductKind, "商品管理员"),
    };

    /// <summary>是否已登录任意一类管理员。</summary>
    public static bool IsLoggedIn(ISession session)
        => !string.IsNullOrEmpty(session.GetString("AdminId"));

    /// <summary>取当前管理员种类（缺省视为账号管理员）。</summary>
    public static string GetKind(ISession session)
        => session.GetString(KindKey) ?? AccountKind;

    public static string KindName(string? kind)
    {
        var hit = Array.Find(Kinds, k => k.Code == kind);
        return hit.Name ?? kind ?? "管理员";
    }

    public static bool IsValidKind(string? kind)
        => !string.IsNullOrEmpty(kind) && Array.Exists(Kinds, k => k.Code == kind);

    /// <summary>是否为某一种管理员。</summary>
    public static bool IsKind(ISession session, string kind)
        => string.Equals(session.GetString(KindKey), kind, StringComparison.OrdinalIgnoreCase);

    /// <summary>是否为「商品管理员」（C 组后台登录，用于打开 A 组平台级页面）。</summary>
    public static bool IsProductAdmin(ISession session)
        => IsLoggedIn(session) && IsKind(session, ProductKind);

    /// <summary>退出登录：清理管理员会话。</summary>
    public static void SignOut(ISession session)
    {
        session.Remove("AdminId");
        session.Remove("AdminName");
        session.Remove(KindKey);
    }
}
