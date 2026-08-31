namespace FreshColdChain.Services;

/// <summary>
/// 可安全反馈给调用方的订单业务异常。
/// </summary>
public sealed class OrderBusinessException : Exception
{
    public OrderBusinessException(string message) : base(message)
    {
    }
}
