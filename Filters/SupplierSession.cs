using Microsoft.AspNetCore.Http;

namespace FreshColdChain.Filters;

/// <summary>
/// 供应商登录态助手。
/// 角色体系：供应商是一种身份，内部靠账号区分「普通供应商」和「供应商管理员」。
/// - 普通供应商：Session["SupplierId"] 存在，只能操作自己家货物；
/// - 供应商管理员：固定账号（AdminAccount）登录，Session["IsSupplierAdmin"]="true"，拥有全部货物/物品/运费模板权限。
/// 与 B 组/C 组的平台管理员（Session["AdminName"]）是两套独立角色，互不相干。
/// </summary>
public static class SupplierSession
{
    /// <summary>供应商管理员固定账号</summary>
    public const string AdminAccount = "admin";

    public const string SupplierIdKey = "SupplierId";
    public const string SupplierNameKey = "SupplierName";
    public const string IsAdminKey = "IsSupplierAdmin";

    /// <summary>是否为供应商管理员（固定账号登录）</summary>
    public static bool IsSupplierAdmin(ISession session)
        => string.Equals(session.GetString(IsAdminKey), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否供应商（普通供应商或供应商管理员）</summary>
    public static bool IsSupplier(ISession session)
        => !string.IsNullOrEmpty(session.GetString(SupplierIdKey)) || IsSupplierAdmin(session);

    /// <summary>当前供应商 ID（供应商管理员可能为 null）</summary>
    public static string? GetSupplierId(ISession session) => session.GetString(SupplierIdKey);
}
