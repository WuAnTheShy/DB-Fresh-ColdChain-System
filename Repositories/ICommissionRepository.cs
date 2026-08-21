using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface ICommissionRepository
    {
        Task<string> InsertAsync(CommissionRecord record, IDbTransaction? transaction = null);
        Task<bool> UpdateStatusAsync(string recordId, string newStatus, IDbTransaction? transaction = null);
        Task<CommissionRecord?> GetByOrderIdAsync(string orderId, IDbTransaction? transaction = null);
        List<CommissionRecord> GetByPromoterId(string promoterId, string? statusFilter = null);
        Task<bool> UpdateRefundedAmountAsync(string recordId, decimal deltaAmount, IDbTransaction? transaction = null);
    }
}
