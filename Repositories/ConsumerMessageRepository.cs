using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>由 B 组订单持久记录生成消费者长期可查消息，不跨组读取 C 组表。</summary>
public sealed class ConsumerMessageRepository(IConfiguration configuration) : B_BaseRepository(configuration), IConsumerMessageRepository
{
    public async Task<List<ConsumerMessage>> GetOrderMessagesAsync(string customerId, int take = 100)
    {
        take = Math.Clamp(take, 1, 100);
        return await WithConnectionAsync(null, async connection =>
            (await connection.QueryAsync<ConsumerMessage>(
                @"SELECT o.OrderId AS MessageId, 'ORDER' AS MessageType,
                         CASE o.OrderStatus WHEN 'PENDING_PAYMENT' THEN '订单待支付'
                            WHEN 'PAID' THEN '支付成功' WHEN 'SHIPPED' THEN '订单已发货'
                            WHEN 'COMPLETED' THEN '订单已完成' WHEN 'CANCELLED' THEN '订单已取消'
                            ELSE '订单状态更新' END AS Title,
                         '订单号：' || o.OrderNo || '，当前状态：' || o.OrderStatus AS Content,
                         o.OrderId, NVL(o.UpdatedAt, o.CreatedAt) AS CreatedAt
                  FROM Biz_Orders o
                  WHERE o.CustomerId = :CustomerId
                  ORDER BY CreatedAt DESC, MessageId DESC
                  FETCH FIRST :Take ROWS ONLY",
                new { CustomerId = customerId, Take = take })).ToList());
    }
}
