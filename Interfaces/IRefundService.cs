using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.CrossGroup_C;
namespace FreshColdChain.Interfaces
{
    public interface IRefundService
    {
        //整体处理退款函数（自建事务)
        //B组直接使用，内含退款所有逻辑（免审核，直接执行）
        public Task<Result> Refund(GroupC_RefundRequest refundRequest);

        //消费者申请退款：仅校验并创建待审核申请单，不动任何资金（自建事务）
        public Task<Result> ApplyRefund(GroupC_RefundRequest refundRequest);

        Task<Result> ApplyCheckoutBatchRefundAsync(string checkoutBatchId, string customerId, string remark);

        //管理员审核退款申请：通过则执行退款资金操作，驳回则仅更新申请单状态（自建事务，幂等）
        public Task<Result> AuditRefund(string refundId, bool approved, string auditorId, string? auditRemark = null);

        //管理端：查询所有待审核退款申请
        public Task<List<FinRefund>> GetPendingRefundsAsync();

        //消费者端：查询某订单的全部退款申请记录
        public Task<List<FinRefund>> GetOrderRefundsAsync(string orderId);

        //管理端：组合查询退款记录（时间区间 [startTime, endTime) + 订单号 + 审核状态）
        public Task<List<FinRefund>> SearchRefundsAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status);
    }
}
