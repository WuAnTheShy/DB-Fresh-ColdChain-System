using System.Data;
using Dapper;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Repositories;

public class PromoterRepository : IPromoterRepository
{
    private readonly IUnitOfWork _uow;

    public PromoterRepository(IUnitOfWork uow)
    {
        _uow = uow;
    }

    // B组兼容实现
    public async Task<CrmPromoter?> GetByIdAsync(int promoterId, IDbTransaction? transaction = null)
    {
        return await _uow.Connection.QueryFirstOrDefaultAsync<CrmPromoter>(
            "SELECT * FROM Crm_Promoters WHERE PromoterId = :PromoterId",
            new { PromoterId = promoterId },
            transaction);
    }

    public async Task<CrmPromoter?> FindByInviteCodeAndNameAsync(string inviteCode, string promoterName, IDbTransaction? transaction = null)
    {
        return await _uow.Connection.QueryFirstOrDefaultAsync<CrmPromoter>(
            @"SELECT * FROM Crm_Promoters
              WHERE InviteCode = :InviteCode AND PromoterName = :PromoterName",
            new { InviteCode = inviteCode, PromoterName = promoterName },
            transaction);
    }

    public async Task<bool> TryAddPendingCommissionAsync(int promoterId, decimal baseAmount, decimal bonusAmount, decimal salesAmount, IDbTransaction? transaction = null)
    {
        var affected = await _uow.Connection.ExecuteAsync(
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
    }

    public async Task<bool> TryActivatePendingCommissionAsync(int promoterId, decimal baseAmount, decimal bonusAmount, IDbTransaction? transaction = null)
    {
        var pendingAmount = baseAmount + bonusAmount;
        var affected = await _uow.Connection.ExecuteAsync(
            @"UPDATE Crm_Promoters
              SET PendingBalance = PendingBalance - :PendingAmount,
                  CurrentBalance = CurrentBalance + :PendingAmount,
                  UpdatedAt = SYSDATE
              WHERE PromoterId = :PromoterId
                AND PendingBalance >= :PendingAmount",
            new { PromoterId = promoterId, PendingAmount = pendingAmount },
            transaction);
        return affected == 1;
    }

    public async Task<bool> TryRollbackCommissionAsync(int promoterId, decimal baseAmount, decimal bonusAmount, decimal salesAmount, IDbTransaction? transaction = null)
    {
        var pendingAmount = baseAmount + bonusAmount;
        var affected = await _uow.Connection.ExecuteAsync(
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
    }

    // C组实现
    public async Task<GroupC_CrmPromoter?> GroupC_FindPromoterRecordAsync(string? promoterId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT
                PROMOTERID as PromoterId,
                PROMOTERNAME as PromoterName,
                PHONE as Phone,
                INVITECODE as InviteCode,
                BASECOMMISSIONRATE as BaseCommissionRate,
                CURRENTBALANCE as CurrentBalance,
                PENDINGBALANCE as PendingBalance,
                NVL(FROZENAMOUNT, 0) as FrozenAmount,
                TOTALSALES as TotalSales,
                TOTALORDERCOUNT as TotalOrderCount,
                STATUS as Status,
                REGISTERTIME as RegisterTime,
                LASTSETTLEMENTTIME as LastSettlementTime,
                REMARK as Remark,
                LOGINACCOUNT as LoginAccount,
                LOGINPASSWORD as LoginPassword,
                AVATAR as Avatar
            FROM CRM_PROMOTERS
            WHERE PROMOTERID = :PromoterId";
        return await _uow.Connection.QueryFirstOrDefaultAsync<GroupC_CrmPromoter>(sql, new { PromoterId = promoterId }, transaction);
    }

    public GroupC_CrmPromoter? GroupC_FindPromoterRecord(string? promoterId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT
                PROMOTERID as PromoterId,
                PROMOTERNAME as PromoterName,
                PHONE as Phone,
                INVITECODE as InviteCode,
                BASECOMMISSIONRATE as BaseCommissionRate,
                CURRENTBALANCE as CurrentBalance,
                PENDINGBALANCE as PendingBalance,
                NVL(FROZENAMOUNT, 0) as FrozenAmount,
                TOTALSALES as TotalSales,
                TOTALORDERCOUNT as TotalOrderCount,
                STATUS as Status,
                REGISTERTIME as RegisterTime,
                LASTSETTLEMENTTIME as LastSettlementTime,
                REMARK as Remark,
                LOGINACCOUNT as LoginAccount,
                LOGINPASSWORD as LoginPassword,
                AVATAR as Avatar
            FROM CRM_PROMOTERS
            WHERE PROMOTERID = :PromoterId";

        return _uow.Connection.QueryFirstOrDefault<GroupC_CrmPromoter>(sql, new { PromoterId = promoterId }, transaction);
    }

    public async Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetPromotersByStatusAsync(string status)
    {
        const string sql = "SELECT * FROM CRM_PROMOTERS WHERE STATUS = :Status";
        return await _uow.Connection.QueryAsync<GroupC_CrmPromoter>(sql, new { Status = status });
    }

    public async Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetAllPromotersAsync()
    {
        const string sql = @"
            SELECT
                PROMOTERID as PromoterId,
                PROMOTERNAME as PromoterName,
                PHONE as Phone,
                INVITECODE as InviteCode,
                BASECOMMISSIONRATE as BaseCommissionRate,
                CURRENTBALANCE as CurrentBalance,
                PENDINGBALANCE as PendingBalance,
                NVL(FROZENAMOUNT, 0) as FrozenAmount,
                TOTALSALES as TotalSales,
                TOTALORDERCOUNT as TotalOrderCount,
                STATUS as Status,
                REGISTERTIME as RegisterTime,
                LASTSETTLEMENTTIME as LastSettlementTime,
                REMARK as Remark,
                LOGINACCOUNT as LoginAccount,
                LOGINPASSWORD as LoginPassword,
                AVATAR as Avatar
            FROM CRM_PROMOTERS
            ORDER BY REGISTERTIME DESC";

        return await _uow.Connection.QueryAsync<GroupC_CrmPromoter>(sql);
    }

    public async Task GroupC_UpdatePromoterTotalSalesAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET TOTALSALES = TOTALSALES + :DeltaAmount
            WHERE PROMOTERID = :PromoterId";

        await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, DeltaAmount = deltaAmount }, transaction);
    }

    public async Task GroupC_UpdatePromoterPendingBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET PENDINGBALANCE = PENDINGBALANCE + :DeltaAmount
            WHERE PROMOTERID = :PromoterId";

        await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, DeltaAmount = deltaAmount }, transaction);
    }

    public async Task<bool> GroupC_UpdatePromoterStatusAsync(string promoterId, string newStatus, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET STATUS = :NewStatus
            WHERE PROMOTERID = :PromoterId";

        var rows = await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, NewStatus = newStatus }, transaction);
        return rows > 0;
    }

    public async Task<bool> GroupC_UpdatePromoterCommissionRateAsync(string promoterId, decimal rate, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET BASECOMMISSIONRATE = :BaseCommissionRate
            WHERE PROMOTERID = :PromoterId";

        var rows = await _uow.Connection.ExecuteAsync(
            sql,
            new { PromoterId = promoterId, BaseCommissionRate = rate },
            transaction);
        return rows > 0;
    }

    public async Task<bool> GroupC_UpdatePromoterAvatarAsync(string promoterId, string? avatar, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET AVATAR = :Avatar
            WHERE PROMOTERID = :PromoterId";

        var rows = await _uow.Connection.ExecuteAsync(
            sql,
            new { PromoterId = promoterId, Avatar = avatar },
            transaction);
        return rows > 0;
    }

    public async Task<decimal?> GroupC_FindPromoterPendingBalanceAsync(string? promoterId, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT PENDINGBALANCE
            FROM CRM_PROMOTERS
            WHERE PROMOTERID = :PromoterId";

        return await _uow.Connection.QueryFirstOrDefaultAsync<decimal?>(sql, new { PromoterId = promoterId }, transaction);
    }

    public async Task GroupC_UpdatePromoterCurrentBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE CRM_PROMOTERS
            SET CURRENTBALANCE = NVL(CURRENTBALANCE, 0) + :DeltaAmount
            WHERE PROMOTERID = :PromoterId";

        await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, DeltaAmount = deltaAmount }, transaction);
    }

    public async Task<bool> GroupC_ExistsPromoterByLoginAccountAsync(string loginAccount, IDbTransaction? transaction = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM CRM_PROMOTERS
            WHERE LOGINACCOUNT = :LoginAccount";

        var count = await _uow.Connection.ExecuteScalarAsync<int>(sql, new { LoginAccount = loginAccount }, transaction);
        return count > 0;
    }

    public GroupC_CrmPromoter? GroupC_FindPromoterByLoginAccount(string loginAccount)
    {
        const string sql = @"
            SELECT
                PROMOTERID as PromoterId,
                PROMOTERNAME as PromoterName,
                PHONE as Phone,
                INVITECODE as InviteCode,
                BASECOMMISSIONRATE as BaseCommissionRate,
                CURRENTBALANCE as CurrentBalance,
                PENDINGBALANCE as PendingBalance,
                NVL(FROZENAMOUNT, 0) as FrozenAmount,
                TOTALSALES as TotalSales,
                TOTALORDERCOUNT as TotalOrderCount,
                STATUS as Status,
                REGISTERTIME as RegisterTime,
                LASTSETTLEMENTTIME as LastSettlementTime,
                REMARK as Remark,
                LOGINACCOUNT as LoginAccount,
                LOGINPASSWORD as LoginPassword,
                AVATAR as Avatar
            FROM CRM_PROMOTERS
            WHERE LOGINACCOUNT = :LoginAccount";

        return _uow.Connection.QueryFirstOrDefault<GroupC_CrmPromoter>(sql, new { LoginAccount = loginAccount });
    }

    public async Task<bool> GroupC_InsertPromoterAsync(GroupC_CrmPromoter promoter, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO CRM_PROMOTERS (
                PROMOTERID, PROMOTERNAME, PHONE, INVITECODE, BASECOMMISSIONRATE,
                CURRENTBALANCE, PENDINGBALANCE, TOTALSALES, TOTALORDERCOUNT, STATUS,
                REGISTERTIME, LASTSETTLEMENTTIME, REMARK, LOGINACCOUNT, LOGINPASSWORD
            ) VALUES (
                :PromoterId, :PromoterName, :Phone, :InviteCode, :BaseCommissionRate,
                :CurrentBalance, :PendingBalance, :TotalSales, :TotalOrderCount, :Status,
                :RegisterTime, :LastSettlementTime, :Remark, :LoginAccount, :LoginPassword
            )";

        var rows = await _uow.Connection.ExecuteAsync(sql, promoter, transaction);
        return rows > 0;
    }

    public async Task<GroupC_PromoterListResult> GetAvailablePromotersAsync(string? keyword, int skip, int take, IDbTransaction? transaction = null)
    {
        var parameters = new { Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim() };

        const string countSql = @"
            SELECT COUNT(*)
            FROM CRM_PROMOTERS
            WHERE UPPER(STATUS) IN ('ENABLE', 'ENABLED', 'ACTIVE')
              AND INSTR(PROMOTERID || CHR(1) || PROMOTERNAME, NVL(:Keyword, CHR(1))) > 0";

        var totalCount = await _uow.Connection.ExecuteScalarAsync<int>(countSql, parameters, transaction);

        const string dataSql = @"
            SELECT PROMOTERID AS PromoterId, PROMOTERNAME AS PromoterName
            FROM CRM_PROMOTERS
            WHERE UPPER(STATUS) IN ('ENABLE', 'ENABLED', 'ACTIVE')
              AND INSTR(PROMOTERID || CHR(1) || PROMOTERNAME, NVL(:Keyword, CHR(1))) > 0
            ORDER BY PROMOTERID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY";

        var items = await _uow.Connection.QueryAsync<GroupC_AvailablePromoterDto>(
            dataSql,
            new { Keyword = parameters.Keyword, Skip = skip, Take = take },
            transaction);

        return new GroupC_PromoterListResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
