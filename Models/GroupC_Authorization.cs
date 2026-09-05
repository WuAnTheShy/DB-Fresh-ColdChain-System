namespace FreshColdChain.Models;

public static class GroupBPermissions
{
    public const string OrdersRead = "groupb.orders.read";
    public const string OrdersManage = "groupb.orders.manage";
    public const string FulfillmentRead = "groupb.fulfillment.read";
    public const string FulfillmentWrite = "groupb.fulfillment.write";
}

public sealed class GroupCAuthorizationResult
{
    public bool IsAllowed { get; init; }
    public string SubjectId { get; init; } = string.Empty;
    public string RoleCode { get; init; } = string.Empty;
    public string? DenialReason { get; init; }
}

/// <summary>现有 SYS_ROLES 没有权限列，因此显式配置角色 ID 对应权限，未配置角色默认拒绝。</summary>
public sealed class GroupCAuthorizationOptions
{
    public const string SectionName = "GroupC:Authorization";
    public Dictionary<string, string[]> RolePermissions { get; set; } = new(StringComparer.Ordinal);
    public string[] SupplierPermissions { get; set; } = [];
}

/// <summary>每个受保护动作显式声明权限，未声明动作不能被过滤器默认放行。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class GroupBPermissionAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}
