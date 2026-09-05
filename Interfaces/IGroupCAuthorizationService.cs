using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>C 组统一操作授权；调用方必须传服务端登录身份，不能使用表单身份。</summary>
public interface IGroupCAuthorizationService
{
    Task<GroupCAuthorizationResult> AuthorizeAsync(string subjectId, string permissionCode,
        string? resourceId = null, CancellationToken cancellationToken = default);
}
