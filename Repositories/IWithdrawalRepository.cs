using DBFreshColdChain.Models.DTOs;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public interface IWithdrawalRepository
    {
        Task<bool> GroupC_HasPendingWithdrawalAsync(string promoterId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<bool> GroupC_InsertWithdrawalRecordAsync(GroupC_FinWithdrawalRecord record, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<bool> GroupC_UpdateWithdrawalStatusAsync(
            string withdrawalId,
            string status,
            string auditorUserId,
            DateTime? auditTime,
            DateTime? transferTime,
            string? rejectReason = null,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default);
        Task<GroupC_FinWithdrawalRecord?> GroupC_GetWithdrawalRecordAsync(string withdrawalId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task GroupC_UpdatePromoterFrozenAmountAsync(string promoterId, decimal delta, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}