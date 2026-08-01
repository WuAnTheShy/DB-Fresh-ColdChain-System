using Dapper;
using DBFreshColdChain.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace DBFreshColdChain.Repositories
{
    public class DbHelper
    {
        private readonly string? _connectionString;

        public DbHelper(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb");
        }

        // 供所有 Service 调用的执行 SQL 方法
        public OracleConnection GetConnection()
        {
            return new OracleConnection(_connectionString);
        }

        // ========== 原有方法（完全保留） ==========

        public void GroupC_AddLogRecord(Log_Auditrails logData)
        {
            string sql = @"
                INSERT INTO LOG_AUDITTRAILS (
                    LOGID, 
                    TABLENAME, 
                    RECORDID, 
                    ACTIONTYPE, 
                    OLDVALUE, 
                    NEWVALUE, 
                    OPERATORTYPE, 
                    OPERATORID, 
                    OPTIME
                ) VALUES (
                    :LogId, 
                    :TableName, 
                    :RecordId, 
                    :ActionType,
                    :OldValue, 
                    :NewValue, 
                    :OperatorType, 
                    :OperatorId, 
                    :OpTime
                )";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, logData);
            }
        }

        public CrmPromoter? GroupC_FindPromoterRecord(string? promoterId)
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
                    LEVELNAME as LevelName,
                    STATUS as Status,
                    REGISTERTIME as RegisterTime,
                    LASTSETTLEMENTTIME as LastSettlementTime,
                    REMARK as Remark,
                    LOGINACCOUNT as LoginAccount,
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                return connection.QueryFirstOrDefault<CrmPromoter>(sql, new { PromoterId = promoterId });
            }
        }

        public void GroupC_UpdatePromoterTotalSales(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET TOTALSALES = TOTALSALES + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new
                {
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }
        }

        public void GroupC_UpdatePromoterPendingBalance(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET PENDINGBALANCE = PENDINGBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new
                {
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }
        }

        public decimal? GroupC_FindPromoterPendingBalance(string? promoterId)
        {
            string sql = @"
                SELECT PENDINGBALANCE
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";
            using (var connection = new OracleConnection(_connectionString))
            {
                return connection.QueryFirstOrDefault<decimal?>(sql, new { PromoterId = promoterId });
            }
        }

        public void GroupC_UpdatePromoterCurrentBalance(string? promoterId, decimal deltaAmount)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET CURRENTBALANCE = CURRENTBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new
                {
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }
        }

        public void GroupC_AddPaymentRecord(FinPaymentRecord finPaymentRecord)
        {
            string sql = @"
                INSERT INTO FIN_PAYMENTRECORDS (
                    PAYID, 
                    ORDERID, 
                    PAYMETHOD, 
                    TRANSACTIONNO, 
                    PAYAMOUNT, 
                    STATUS, 
                    PAYTIME, 
                    REMARK
                ) VALUES (
                    :PayId, 
                    :OrderId, 
                    :PayMethod, 
                    :TransactionNo, 
                    :PayAmount, 
                    :Status, 
                    :PayTime, 
                    :Remark
                )";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, finPaymentRecord);
            }
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

            using (var connection = new OracleConnection(_connectionString))
            {
                int count = connection.ExecuteScalar<int>(sql, new { LoginAccount = loginAccount });
                return count > 0;
            }
        }

        /// <summary>
        /// 根据登录账号获取团长信息（用于登录验证）
        /// </summary>
        public CrmPromoter? GroupC_FindPromoterByLoginAccount(string loginAccount)
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
                    LEVELNAME as LevelName,
                    STATUS as Status,
                    REGISTERTIME as RegisterTime,
                    LASTSETTLEMENTTIME as LastSettlementTime,
                    REMARK as Remark,
                    LOGINACCOUNT as LoginAccount,
                    LOGINPASSWORD as LoginPassword
                FROM CRM_PROMOTERS
                WHERE LOGINACCOUNT = :LoginAccount";

            using (var connection = new OracleConnection(_connectionString))
            {
                return connection.QueryFirstOrDefault<CrmPromoter>(sql, new { LoginAccount = loginAccount });
            }
        }

        /// <summary>
        /// 插入新的团长记录
        /// </summary>
        public bool GroupC_InsertPromoter(CrmPromoter promoter)
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
                    LEVELNAME,
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
                    :LevelName,
                    :Status,
                    :RegisterTime,
                    :LastSettlementTime,
                    :Remark,
                    :LoginAccount,
                    :LoginPassword
                )";

            using (var connection = new OracleConnection(_connectionString))
            {
                int rows = connection.Execute(sql, promoter);
                return rows > 0;
            }
        }
    }
}