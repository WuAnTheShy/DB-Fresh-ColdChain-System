using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models.CrossGroup_C;
using System.Data;
namespace FreshColdChain.Interfaces
{
    public interface IPaymentService
    {
        // 创建支付流水函数接口
        Task<Result> CreatePaymentRecord(PaymentRequest paymentRequest,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default);

        // 组合查询支付流水（管理端）：时间区间 [startTime, endTime) + 订单号 + 支付状态
        Task<List<GroupC_FinPaymentRecord>> SearchPaymentsAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status);
    }
}
