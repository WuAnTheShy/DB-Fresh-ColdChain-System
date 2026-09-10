namespace FreshColdChain.Services;

// 可安全反馈给调用方的订单业务异常。
public sealed class OrderBusinessException : Exception
{
    public OrderBusinessException(string message) : base(message)
    {
    }
}
