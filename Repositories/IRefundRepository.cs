using System.Data;
using FreshColdChain.Models;


namespace FreshColdChain.Repositories
{
    public interface IRefundRepository
    {
        //需要事务：业务逻辑
        Task InsertRefundAsync(FinRefund refund, IDbTransaction? transaction = null);

        //按退款编号查询申请单
        Task<FinRefund?> GetByIdAsync(string refundId, IDbTransaction? transaction = null);

        //按审核状态查询申请单列表（管理端待审核列表用）
        Task<List<FinRefund>> GetByStatusAsync(string status, IDbTransaction? transaction = null);

        //查询某订单的全部退款申请记录
        Task<List<FinRefund>> GetByOrderIdAsync(string orderId, IDbTransaction? transaction = null);

        //该订单是否存在待审核的退款申请（防重复申请）
        Task<bool> HasPendingApplicationAsync(string orderId, IDbTransaction? transaction = null);

        //带旧状态条件的审核状态更新（乐观锁），同时记录审核人/审核时间/审核意见
        Task<bool> TryUpdateStatusAsync(string refundId, string expectedStatus, string newStatus,
            string? auditorId, string? auditRemark, IDbTransaction? transaction = null);
    }
}
