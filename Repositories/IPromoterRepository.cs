using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using System.Data;

namespace FreshColdChain.Repositories;

public interface IPromoterRepository
{
    // B组兼容接口
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

    // C组接口
    Task<GroupC_CrmPromoter?> GroupC_FindPromoterRecordAsync(string? promoterId, IDbTransaction? transaction = null);
    GroupC_CrmPromoter? GroupC_FindPromoterRecord(string? promoterId, IDbTransaction? transaction = null);
    Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetPromotersByStatusAsync(string status);
    Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetAllPromotersAsync();
    Task GroupC_UpdatePromoterTotalSalesAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null);
    Task GroupC_UpdatePromoterPendingBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null);
    Task<bool> GroupC_UpdatePromoterStatusAsync(string promoterId, string newStatus, IDbTransaction? transaction = null);
    Task<bool> GroupC_UpdatePromoterCommissionRateAsync(string promoterId, decimal rate, IDbTransaction? transaction = null);
    Task<bool> GroupC_UpdatePromoterAvatarAsync(string promoterId, string? avatar, IDbTransaction? transaction = null);
    Task<bool> GroupC_UpdatePromoterPayAccountAsync(string promoterId, string platform, string? accountNo, IDbTransaction? transaction = null);
    Task<decimal?> GroupC_FindPromoterPendingBalanceAsync(string? promoterId, IDbTransaction? transaction = null);
    Task GroupC_UpdatePromoterCurrentBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null);
    Task<bool> GroupC_ExistsPromoterByLoginAccountAsync(string loginAccount, IDbTransaction? transaction = null);
    Task<bool> GroupC_InsertPromoterAsync(GroupC_CrmPromoter promoter, IDbTransaction? transaction = null);
    GroupC_CrmPromoter? GroupC_FindPromoterByLoginAccount(string loginAccount);
    Task<GroupC_PromoterListResult> GetAvailablePromotersAsync(string? keyword, int skip, int take, IDbTransaction? transaction = null);
}
