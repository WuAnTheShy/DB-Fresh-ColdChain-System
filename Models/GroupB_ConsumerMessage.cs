namespace FreshColdChain.Models;

// 消费者消息中心的持久业务事件投影。
public sealed class ConsumerMessage
{
    public string MessageId { get; init; } = string.Empty;
    public string MessageType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? OrderId { get; init; }
    public DateTime CreatedAt { get; init; }
}
