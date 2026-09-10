using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// 通过 B 组订单仓储和 C 组退款服务组合消费者消息，不直接跨组访问数据表。
public sealed class ConsumerMessageService(
    IConsumerMessageRepository repository,
    IRefundService refundService) : IConsumerMessageService
{
    public async Task<List<ConsumerMessage>> GetMessagesAsync(
        string customerId,
        int take = 100)
    {
        take = Math.Clamp(take, 1, 100);
        var orderMessages = await repository.GetOrderMessagesAsync(customerId, take);
        var orderIds = orderMessages
            .Select(message => message.OrderId)
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var refunds = new List<FinRefund>();
        foreach (var orderId in orderIds)
        {
            // C 组服务在当前请求内共享同一工作单元，必须顺序调用，避免并发使用同一连接。
            refunds.AddRange(await refundService.GetOrderRefundsAsync(orderId));
        }
        var refundMessages = refunds.Select(ToMessage);

        return orderMessages
            .Concat(refundMessages)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.MessageId, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    private static ConsumerMessage ToMessage(FinRefund refund)
    {
        var title = refund.Status switch
        {
            "Pending" => "退款申请已提交",
            "Approved" => "退款申请已通过",
            "Rejected" => "退款申请已驳回",
            "Refunded" => "退款已完成",
            _ => "退款状态更新"
        };
        return new ConsumerMessage
        {
            MessageId = refund.RefundId ?? string.Empty,
            MessageType = "REFUND",
            Title = title,
            Content = $"订单退款金额：¥{refund.RefundAmount:0.00}，状态：{refund.Status}",
            OrderId = refund.OrderId,
            CreatedAt = refund.AuditTime ?? refund.ApplyTime
        };
    }
}
