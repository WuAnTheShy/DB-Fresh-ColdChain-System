using Dapper;
using DBFreshColdChain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
        public CrmPromoter? GroupC_FindPromoterRecord(string? promoterId)  //查找团长记录
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
                return connection.QueryFirstOrDefault<CrmPromoter>(sql,new{PromoterId = promoterId });
            }
        }
        public void GroupC_UpdatePromoterTotalSales(string? promoterId,decimal deltaAmount) //增加累计销售额
        {
            
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET TOTALSALES = TOTALSALES + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new {   
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }

        }
        public void GroupC_UpdatePromoterPendingBalance(string? promoterId, decimal deltaAmount)    //更改团长表的待结算余额
        {
           
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET PENDINGBALANCE = PENDINGBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new {
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }
        }
        public decimal? GroupC_FindPromoterPendingBalance(string? promoterId) //查看团长表的待结算余额,如果查不到数据返回 null
        {
            string sql = @"
                SELECT PENDINGBALANCE
                FROM CRM_PROMOTERS
                WHERE PROMOTERID = :PromoterId";
            using (var connection = new OracleConnection(_connectionString))
            {
                // QueryFirstOrDefault<decimal?> 如果表里没查到该行，会返回 null
                return connection.QueryFirstOrDefault<decimal?>(sql, new { PromoterId = promoterId });
            }

        }
        public void GroupC_UpdatePromoterCurrentBalance(string? promoterId, decimal deltaAmount)    //更改团长表的可提现余额
        {
       
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET CURRENTBALANCE = CURRENTBALANCE + :DeltaAmount
                WHERE PROMOTERID = :PromoterId";

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, new {
                    PromoterId = promoterId,
                    DeltaAmount = deltaAmount
                });
            }
        }

        public void GroupC_AddPaymentRecord(FinPaymentRecord finPaymentRecord)  //添加支付流水记录
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
    }
}
