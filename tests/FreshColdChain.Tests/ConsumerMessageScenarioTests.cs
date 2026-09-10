using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class ConsumerMessageScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        try
        {
            await MessagesAreComposedAcrossGroupServicesAsync();
            Console.WriteLine("PASS 消息中心通过跨组服务组合订单与退款消息");
            Console.WriteLine("消息中心场景总数: 1, 通过: 1, 失败: 0");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL 消息中心通过跨组服务组合订单与退款消息");
            Console.WriteLine(exception);
            Console.WriteLine("消息中心场景总数: 1, 通过: 0, 失败: 1");
            return 1;
        }
    }

    private static async Task MessagesAreComposedAcrossGroupServicesAsync()
    {
        var repository = new StubConsumerMessageRepository
        {
            Messages =
            [
                OrderMessage("order-1", new DateTime(2026, 8, 31, 10, 0, 0)),
                OrderMessage("order-2", new DateTime(2026, 8, 31, 8, 0, 0))
            ]
        };
        var refundService = new StubRefundService
        {
            Refunds =
            [
                Refund("refund-1", "order-1", "Approved", 12.5m,
                    new DateTime(2026, 8, 31, 11, 0, 0)),
                Refund("refund-2", "order-2", "Pending", 8m,
                    new DateTime(2026, 8, 31, 9, 0, 0))
            ]
        };
        var service = new ConsumerMessageService(repository, refundService);

        var messages = await service.GetMessagesAsync(TestIds.Customer, 3);

        AssertEx.Equal(3, messages.Count);
        AssertEx.Equal("refund-1", messages[0].MessageId);
        AssertEx.Equal("order-1", messages[1].MessageId);
        AssertEx.Equal("refund-2", messages[2].MessageId);
        AssertEx.Equal("退款申请已通过", messages[0].Title);
        AssertEx.Equal(3, repository.LastTake);
        AssertEx.True(refundService.RequestedOrderIds.SetEquals(["order-1", "order-2"]));
    }

    private static ConsumerMessage OrderMessage(string orderId, DateTime createdAt) => new()
    {
        MessageId = orderId,
        MessageType = "ORDER",
        Title = "订单状态更新",
        OrderId = orderId,
        CreatedAt = createdAt
    };

    private static FinRefund Refund(
        string refundId,
        string orderId,
        string status,
        decimal amount,
        DateTime createdAt) => new()
    {
        RefundId = refundId,
        OrderId = orderId,
        Status = status,
        RefundAmount = amount,
        ApplyTime = createdAt
    };

    private sealed class StubConsumerMessageRepository : IConsumerMessageRepository
    {
        public List<ConsumerMessage> Messages { get; init; } = [];
        public int LastTake { get; private set; }

        public Task<List<ConsumerMessage>> GetOrderMessagesAsync(
            string customerId,
            int take = 100)
        {
            LastTake = take;
            return Task.FromResult(Messages.Take(take).ToList());
        }
    }

    private sealed class StubRefundService : IRefundService
    {
        public List<FinRefund> Refunds { get; init; } = [];
        public HashSet<string> RequestedOrderIds { get; } = new(StringComparer.Ordinal);

        public Task<Result> Refund(GroupC_RefundRequest refundRequest) => Success();
        public Task<Result> ApplyRefund(GroupC_RefundRequest refundRequest) => Success();
        public Task<RefundPreviewResult> PreviewRefundAsync(GroupC_RefundRequest refundRequest) =>
            Task.FromResult(new RefundPreviewResult { IsSuccess = true });
        public Task<Result> CancelRefundApplicationAsync(
            string orderId,
            string refundId,
            string customerId) => Success();
        public Task<Result> ApplyCheckoutBatchRefundAsync(
            string checkoutBatchId,
            string customerId,
            string remark) => Success();
        public Task<Result> AuditRefund(
            string refundId,
            bool approved,
            string auditorId,
            string? auditRemark = null) => Success();
        public Task<List<FinRefund>> GetPendingRefundsAsync() =>
            Task.FromResult(new List<FinRefund>());
        public Task<List<FinRefund>> GetOrderRefundsAsync(string orderId)
        {
            RequestedOrderIds.Add(orderId);
            return Task.FromResult(Refunds.Where(refund => refund.OrderId == orderId).ToList());
        }
        public Task<List<FinRefund>> GetOrderRefundsAsync(IReadOnlyCollection<string> orderIds)
        {
            foreach (var orderId in orderIds)
            {
                RequestedOrderIds.Add(orderId);
            }
            return Task.FromResult(Refunds.Where(refund => orderIds.Contains(refund.OrderId!)).ToList());
        }
        public Task<List<string>> GetOrderIdsWithPendingRefundAsync() =>
            Task.FromResult(Refunds
                .Where(refund => refund.Status == "Pending")
                .Select(refund => refund.OrderId!)
                .Distinct(StringComparer.Ordinal)
                .ToList());
        public Task<List<FinRefund>> SearchRefundsAsync(
            DateTime? startTime,
            DateTime? endTime,
            string? orderId,
            string? status) => Task.FromResult(new List<FinRefund>());

        private static Task<Result> Success() =>
            Task.FromResult(new Result { IsSuccess = true });
    }
}
