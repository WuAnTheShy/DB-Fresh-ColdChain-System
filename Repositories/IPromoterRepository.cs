using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPromoterRepository
{
    Task<CrmPromoter?> GetByIdAsync(int promoterId, IDbTransaction? transaction = null);

    Task<CrmPromoter?> FindByInviteCodeAndNameAsync(
        string inviteCode,
        string promoterName,
        IDbTransaction? transaction = null);

    Task<bool> TryAddPendingCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        decimal salesAmount,
        IDbTransaction? transaction = null);

    Task<bool> TryActivatePendingCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        IDbTransaction? transaction = null);

    Task<bool> TryRollbackCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        decimal salesAmount,
        IDbTransaction? transaction = null);
}