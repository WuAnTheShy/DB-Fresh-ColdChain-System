using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public sealed class PromoterRepository : BaseRepository, IPromoterRepository
{
    public PromoterRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<CrmPromoter?> GetByIdAsync(int promoterId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmPromoter>(
                "SELECT * FROM Crm_Promoters WHERE PromoterId = :PromoterId",
                new { PromoterId = promoterId },
                transaction));
    }

    public async Task<CrmPromoter?> FindByInviteCodeAndNameAsync(
        string inviteCode,
        string promoterName,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmPromoter>(
                @"SELECT * FROM Crm_Promoters
                  WHERE InviteCode = :InviteCode AND PromoterName = :PromoterName",
                new { InviteCode = inviteCode, PromoterName = promoterName },
                transaction));
    }

    public async Task<bool> TryAddPendingCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        decimal salesAmount,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Promoters
                  SET PendingBalance = PendingBalance + :PendingAmount,
                      TotalSales = TotalSales + :SalesAmount,
                      TotalOrderCount = TotalOrderCount + 1,
                      UpdatedAt = SYSDATE
                  WHERE PromoterId = :PromoterId",
                new
                {
                    PromoterId = promoterId,
                    PendingAmount = baseAmount + bonusAmount,
                    SalesAmount = salesAmount
                },
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> TryActivatePendingCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var pendingAmount = baseAmount + bonusAmount;
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Promoters
                  SET PendingBalance = PendingBalance - :PendingAmount,
                      CurrentBalance = CurrentBalance + :PendingAmount,
                      UpdatedAt = SYSDATE
                  WHERE PromoterId = :PromoterId
                    AND PendingBalance >= :PendingAmount",
                new { PromoterId = promoterId, PendingAmount = pendingAmount },
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> TryRollbackCommissionAsync(
        int promoterId,
        decimal baseAmount,
        decimal bonusAmount,
        decimal salesAmount,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var pendingAmount = baseAmount + bonusAmount;
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Promoters
                  SET PendingBalance = GREATEST(PendingBalance - :PendingAmount, 0),
                      TotalSales = GREATEST(TotalSales - :SalesAmount, 0),
                      TotalOrderCount = GREATEST(TotalOrderCount - 1, 0),
                      UpdatedAt = SYSDATE
                  WHERE PromoterId = :PromoterId",
                new
                {
                    PromoterId = promoterId,
                    PendingAmount = pendingAmount,
                    SalesAmount = salesAmount
                },
                transaction);
            return affected == 1;
        });
    }
}