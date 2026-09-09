using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface ICommissionRepository
    {
        Task<string> InsertAsync(CommissionRecord record, IDbTransaction? transaction = null);
        Task<bool> UpdateStatusAsync(string recordId, string newStatus, IDbTransaction? transaction = null);
        // 带旧状态条件的原子状态更新（乐观锁），返回是否实际更新成功
        Task<bool> TryUpdateStatusAsync(string recordId, string expectedStatus, string newStatus, IDbTransaction? transaction = null);
        // 查询已过退款期（到预计结算时间）仍未结算的佣金记录，供二段结算定时任务扫描
        Task<List<CommissionRecord>> GetDueSettlementsAsync(DateTime now, IDbTransaction? transaction = null);
        Task<CommissionRecord?> GetByOrderIdAsync(string orderId, IDbTransaction? transaction = null);
        List<CommissionRecord> GetByPromoterId(string promoterId, string? statusFilter = null);
        Task<bool> UpdateRefundedAmountAsync(string recordId, decimal deltaAmount, IDbTransaction? transaction = null);
    }
}
