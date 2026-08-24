using Dapper;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.ViewModels;
using FreshColdChain.Repositories;
using System.Data;
using System.Transactions;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace FreshColdChain.Repositories
{
    public class PromoterRepository : IPromoterRepository
    {
        private readonly IUnitOfWork _uow;

        public PromoterRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<GroupC_CrmPromoter?> GroupC_FindPromoterRecordAsync(string? promoterId, IDbTransaction? transaction = null)
        {
            string sql = @"
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
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";

            return await _uow.Connection.QueryFirstOrDefaultAsync<GroupC_CrmPromoter>(sql, new { PromoterId = promoterId }, transaction);
        }
        public GroupC_CrmPromoter? GroupC_FindPromoterRecord(string? promoterId, IDbTransaction? transaction = null)
        {
            string sql = @"
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
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";

            return _uow.Connection.QueryFirstOrDefault<GroupC_CrmPromoter>(sql, new { PromoterId = promoterId }, transaction);
        }

        //查找全部团长（管理端启禁用列表用）
        public async Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetAllPromotersAsync()
        {
            string sql = @"
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
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                ORDER BY REGISTERTIME DESC";

            return await _uow.Connection.QueryAsync<GroupC_CrmPromoter>(sql);
        }

        public async Task GroupC_UpdatePromoterTotalSalesAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET TOTALSALES = TOTALSALES + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            await _uow.Connection.ExecuteAsync(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            }, transaction);
        }

        public async Task GroupC_UpdatePromoterPendingBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET PENDINGBALANCE = PENDINGBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            await _uow.Connection.ExecuteAsync(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            }, transaction);
        }
        public async Task<bool> GroupC_UpdatePromoterStatusAsync(string promoterId, string newStatus, IDbTransaction? transaction = null)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET STATUS = :NewStatus
                WHERE PROMOTERID = :PromoterId";

            int rows = await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, NewStatus = newStatus }, transaction);
            return rows > 0;
        }
        public async Task<IEnumerable<GroupC_CrmPromoter>> GroupC_GetPromotersByStatusAsync(string status)
        {
            string sql = "SELECT * FROM CRM_PROMOTERS WHERE STATUS = :Status";
            return await _uow.Connection.QueryAsync<GroupC_CrmPromoter>(sql, new { Status = status });
        }
        public async Task<decimal?> GroupC_FindPromoterPendingBalanceAsync(string? promoterId, IDbTransaction? transaction = null)
        {
            string sql = @"
                SELECT PENDINGBALANCE
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";

            return await _uow.Connection.QueryFirstOrDefaultAsync<decimal?>(sql, new { PromoterId = promoterId }, transaction);
        }

        public async Task GroupC_UpdatePromoterCurrentBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET CURRENTBALANCE = NVL(CURRENTBALANCE, 0) + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            await _uow.Connection.ExecuteAsync(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            }, transaction);
        }

        public async Task<bool> GroupC_ExistsPromoterByLoginAccountAsync(string loginAccount, IDbTransaction? transaction = null)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM CRM_PROMOTERS 
                WHERE LOGINACCOUNT = :LoginAccount";

            int count = await _uow.Connection.ExecuteScalarAsync<int>(sql, new { LoginAccount = loginAccount }, transaction);
            return count > 0;
        }

        public GroupC_CrmPromoter? GroupC_FindPromoterByLoginAccount(string loginAccount)
        {
            string sql = @"
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
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                WHERE LOGINACCOUNT = :LoginAccount";

            return _uow.Connection.QueryFirstOrDefault<GroupC_CrmPromoter>(sql, new { LoginAccount = loginAccount });
        }

        public async Task<bool> GroupC_InsertPromoterAsync(GroupC_CrmPromoter promoter, IDbTransaction? transaction = null)
        {
            string sql = @"
                INSERT INTO CRM_PROMOTERS (
                    PROMOTERID,
                    PROMOTERNAME,
                    PHONE,
                    INVITECODE,
                    BASECOMMISSIONRATE,
                    CURRENTBALANCE,
                    PENDINGBALANCE,
                    TOTALSALES,
                    TOTALORDERCOUNT,
                    STATUS,
                    REGISTERTIME,
                    LASTSETTLEMENTTIME,
                    REMARK,
                    LOGINACCOUNT,
                    LOGINPASSWORD
                ) VALUES (
                    :PromoterId,
                    :PromoterName,
                    :Phone,
                    :InviteCode,
                    :BaseCommissionRate,
                    :CurrentBalance,
                    :PendingBalance,
                    :TotalSales,
                    :TotalOrderCount,
                    :Status,
                    :RegisterTime,
                    :LastSettlementTime,
                    :Remark,
                    :LoginAccount,
                    :LoginPassword
                )";

            int rows = await _uow.Connection.ExecuteAsync(sql, promoter, transaction);
            return rows > 0;
        }


        public async Task<GroupC_PromoterListResult> GetAvailablePromotersAsync(
        string? keyword,
        int skip,
        int take,
        IDbTransaction? transaction = null)
        {
            var parameters = new
            {
                Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim()
            };

            var countSql = @"
            SELECT COUNT(*)
            FROM CRM_PROMOTERS
            WHERE UPPER(STATUS) IN ('ENABLE', 'ENABLED', 'ACTIVE')
            AND INSTR(PROMOTERID || CHR(1) || PROMOTERNAME, NVL(:Keyword, CHR(1))) > 0";

            var totalCount = await _uow.Connection.ExecuteScalarAsync<int>(
                countSql, parameters, transaction);

            var dataSql = @"
            SELECT PROMOTERID AS PromoterId, PROMOTERNAME AS PromoterName
            FROM CRM_PROMOTERS
            WHERE UPPER(STATUS) IN ('ENABLE', 'ENABLED', 'ACTIVE')
            AND INSTR(PROMOTERID || CHR(1) || PROMOTERNAME, NVL(:Keyword, CHR(1))) > 0
            ORDER BY PROMOTERID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY";

            var items = await _uow.Connection.QueryAsync<GroupC_AvailablePromoterDto>(
                dataSql, new { Keyword = parameters.Keyword, Skip = skip, Take = take }, transaction);

            return new GroupC_PromoterListResult
            {
                Items = items,
                TotalCount = totalCount
            };
        }
    }
}
