namespace FreshColdChain.Services;

/// <summary>
/// 可安全反馈给页面的 GroupB 业务规则异常。
/// </summary>
public sealed class GroupBBusinessException : Exception
{
    public GroupBBusinessException(string message) : base(message)
    {
    }
}
