using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>由订单和退款持久记录生成消费者长期可查消息，不依赖临时会话。</summary>
public sealed class ConsumerMessageRepository(IConfiguration configuration) : B_BaseRepository(configuration), IConsumerMessageRepository
{
    public async Task<List<ConsumerMessage>> GetMessagesAsync(string customerId, int take = 100)
    {
        take = Math.Clamp(take, 1, 100);
        return await WithConnectionAsync(null, async connection =>
            (await connection.QueryAsync<ConsumerMessage>(
                @"SELECT * FROM (
                    SELECT o.OrderId AS MessageId, 'ORDER' AS MessageType,
                           CASE o.OrderStatus WHEN 'PENDING_PAYMENT' THEN '订单待支付'
                              WHEN 'PAID' THEN '支付成功' WHEN 'SHIPPED' THEN '订单已发货'
                              WHEN 'COMPLETED' THEN '订单已完成' WHEN 'CANCELLED' THEN '订单已取消'
                              ELSE '订单状态更新' END AS Title,
                           '订单号：' || o.OrderNo || '，当前状态：' || o.OrderStatus AS Content,
                           o.OrderId, NVL(o.UpdatedAt, o.CreatedAt) AS CreatedAt
                    FROM Biz_Orders o WHERE o.CustomerId = :CustomerId
                    UNION ALL
                    SELECT r.RefundId AS MessageId, 'REFUND' AS MessageType,
                           CASE r.Status WHEN 'Pending' THEN '退款申请已提交'
                              WHEN 'Approved' THEN '退款申请已通过' WHEN 'Rejected' THEN '退款申请已驳回'
                              WHEN 'Refunded' THEN '退款已完成' ELSE '退款状态更新' END AS Title,
                           '订单退款金额：¥' || TO_CHAR(r.RefundAmount, 'FM999999990.00') || '，状态：' || r.Status AS Content,
                           r.OrderId, NVL(r.AuditTime, r.ApplyTime) AS CreatedAt
                    FROM FIN_REFUND r JOIN Biz_Orders o ON o.OrderId = r.OrderId
                    WHERE o.CustomerId = :CustomerId
                  ) ORDER BY CreatedAt DESC, MessageId DESC
                  FETCH FIRST :Take ROWS ONLY",
                new { CustomerId = customerId, Take = take })).ToList());
    }
}
