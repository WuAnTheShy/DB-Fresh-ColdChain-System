using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

// C 组读取自身用户/角色，通过 A 组服务验证供应商，不让 B 组直读跨组表。
public sealed class GroupCAuthorizationService(ISysAdminRepository admins, ISupplierService suppliers,
    IOptionsSnapshot<GroupCAuthorizationOptions> options) : IGroupCAuthorizationService
{
    public async Task<GroupCAuthorizationResult> AuthorizeAsync(string subjectId, string permissionCode,
        string? resourceId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GroupCAuthorizationResult Deny(string reason, string role = "") => new()
            { SubjectId = subjectId, RoleCode = role, DenialReason = reason };
        if (!GroupBIds.IsValid(subjectId)) return Deny("登录身份无效");

        if (permissionCode is GroupBPermissions.FulfillmentRead or GroupBPermissions.FulfillmentWrite)
        {
            if (!options.Value.SupplierPermissions.Contains(permissionCode, StringComparer.Ordinal))
                return Deny("未授予供应商履约权限", "SUPPLIER");
            // resourceId 在供应商授权中表示供应商数据范围，而不是客户端提供的身份。
            if (!string.Equals(subjectId, resourceId, StringComparison.Ordinal))
                return Deny("不允许访问其他供应商的数据", "SUPPLIER");
            var response = await suppliers.GetSupplierByIdAsync(subjectId);
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccess || response.Data == null || response.Data.SupplierID != subjectId ||
                !string.Equals(response.Data.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                response.Data.ExpiryDate == null || response.Data.ExpiryDate < DateTime.Now)
                return Deny("供应商不存在、未启用或资质已过期", "SUPPLIER");
            return new() { SubjectId = subjectId, RoleCode = "SUPPLIER", IsAllowed = true };
        }

        if (permissionCode is not (GroupBPermissions.OrdersRead or GroupBPermissions.OrdersManage))
            return Deny("未知操作权限");
        var user = await admins.GetUserByIdAsync(subjectId, cancellationToken: cancellationToken);
        if (user == null || user.UserId != subjectId || !IsEnabled(user.Status))
            return Deny("管理员不存在或未启用");
        var role = await admins.GetRoleByIdAsync(user.RoleId, cancellationToken: cancellationToken);
        if (role == null || role.RoleId != user.RoleId || !IsEnabled(role.Status))
            return Deny("管理员角色不存在或未启用");
        if (!options.Value.RolePermissions.TryGetValue(role.RoleId, out var permissions) ||
            permissions == null || !permissions.Contains(permissionCode, StringComparer.Ordinal))
            return Deny("角色未被授予此操作权限", role.RoleCode);
        return new() { SubjectId = subjectId, RoleCode = role.RoleCode, IsAllowed = true };
    }

    private static bool IsEnabled(string status) =>
        string.Equals(status, "Enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Enabled", StringComparison.OrdinalIgnoreCase);
}
