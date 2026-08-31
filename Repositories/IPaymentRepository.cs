using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface IPaymentRepository
    {
        Task GroupC_AddPaymentRecordAsync(GroupC_FinPaymentRecord finPaymentRecord, IDbTransaction? transaction = null);

        //组合查询支付流水：时间区间 [startTime, endTime) + 订单号 + 支付状态，按支付时间倒序
        Task<List<GroupC_FinPaymentRecord>> SearchAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status, IDbTransaction? transaction = null);
    }

}
