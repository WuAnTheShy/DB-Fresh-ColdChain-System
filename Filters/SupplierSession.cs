using Microsoft.AspNetCore.Http;

namespace FreshColdChain.Filters;

/// <summary>
/// 供应商登录态助手。
/// 供应商指入驻平台的商家（Inv_Suppliers 账号，Session["SupplierId"] 存在）；
/// 平台级商品/物品/定价/运费操作改由 C 组「商品管理员」承担
/// （SYS_USERS.ADMIN_KIND='PRODUCT'，见 AdminSession）。
/// 旧 A 组「供应商管理员」固定账号 admin 已随 20260906 迁移删除，不再通过供应商门户登录。
/// </summary>
public static class SupplierSession
{
    public const string SupplierIdKey = "SupplierId";
    public const string SupplierNameKey = "SupplierName";
    public const string IsAdminKey = "IsSupplierAdmin";

    /// <summary>是否为「供应商管理员」会话（历史 A 组固定账号残留，仅旧会话可能存在）。</summary>
    public static bool IsSupplierAdmin(ISession session)
        => string.Equals(session.GetString(IsAdminKey), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否「平台级运营者」：可查看/操作全部供应商数据。
    /// 原供应商管理员已并入 C 组商品管理员，此处对二者一并向平台级操作放行。</summary>
    public static bool IsPlatformAdmin(ISession session)
        => IsSupplierAdmin(session) || AdminSession.IsProductAdmin(session);

    /// <summary>是否供应商（登录了供应商门户的商家账号）。</summary>
    public static bool IsSupplier(ISession session)
        => !string.IsNullOrEmpty(session.GetString(SupplierIdKey)) || IsSupplierAdmin(session);

    /// <summary>当前供应商 ID（供应商管理员可能为 null）。</summary>
    public static string? GetSupplierId(ISession session) => session.GetString(SupplierIdKey);
}
