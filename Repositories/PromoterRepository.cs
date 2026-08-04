using Dapper;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Configuration;
using DBFreshColdChain.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
namespace DBFreshColdChain.Repositories
{
    public class PromoterRepository
    {
        private readonly IUnitOfWork _uow;  // 注入工作单元

        public PromoterRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // 供所有 Service 调用的执行 SQL 方法

        public GroupC_CrmPromoter? GroupC_FindPromoterRecord(string? promoterId)
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

            return _uow.Connection.QueryFirstOrDefault<GroupC_CrmPromoter>(sql, new { PromoterId = promoterId });
        }

        public void GroupC_UpdatePromoterTotalSales(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET TOTALSALES = TOTALSALES + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            _uow.Connection.Execute(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            });
        }

        public void GroupC_UpdatePromoterPendingBalance(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET PENDINGBALANCE = PENDINGBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            _uow.Connection.Execute(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            });
        }

        public decimal? GroupC_FindPromoterPendingBalance(string? promoterId)
        {
            string sql = @"
                SELECT PENDINGBALANCE
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";
            return _uow.Connection.QueryFirstOrDefault<decimal?>(sql, new { PromoterId = promoterId });
        }

        public void GroupC_UpdatePromoterCurrentBalance(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET CURRENTBALANCE = CURRENTBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            _uow.Connection.Execute(sql, new
            {
                PromoterId = promoterId,
                DeltaAmount = deltaAmount
            });
        }
        // ========== 新增方法（供注册/登录/管理员添加使用） ==========

        /// <summary>
        /// 检查登录账号是否已存在
        /// </summary>
        public bool GroupC_ExistsPromoterByLoginAccount(string loginAccount)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM CRM_PROMOTERS 
                WHERE LOGINACCOUNT = :LoginAccount";

            int count = _uow.Connection.ExecuteScalar<int>(sql, new { LoginAccount = loginAccount });
            return count > 0;
        }

        /// <summary>
        /// 根据登录账号获取团长信息（用于登录验证）
        /// </summary>
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

        /// <summary>
        /// 插入新的团长记录
        /// </summary>
        public bool GroupC_InsertPromoter(GroupC_CrmPromoter promoter)
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

            int rows = _uow.Connection.Execute(sql, promoter);
            return rows > 0;
        }
    }

}