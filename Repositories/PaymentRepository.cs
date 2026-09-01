using Dapper;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Configuration;
using FreshColdChain.Models;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;
namespace FreshColdChain.Repositories
{
    public class PaymentRepository: IPaymentRepository
    {
        private readonly IUnitOfWork _uow;  // 注入工作单元

        public PaymentRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // 供所有 Service 调用的执行 SQL 方法
        public async Task GroupC_AddPaymentRecordAsync(GroupC_FinPaymentRecord finPaymentRecord, IDbTransaction? transaction = null)
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

            var connection = transaction?.Connection ?? _uow.Connection;
            await connection.ExecuteAsync(sql, finPaymentRecord, transaction);
        }

        // 组合查询支付流水（管理端查询页用，结果上限 500 条）
        public async Task<List<GroupC_FinPaymentRecord>> SearchAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status, IDbTransaction? transaction = null)
        {
            const string sql = @"
                SELECT * FROM FIN_PAYMENTRECORDS
                WHERE (:StartTime IS NULL OR PAYTIME >= :StartTime)
                  AND (:EndTime IS NULL OR PAYTIME < :EndTime)
                  AND (:OrderId IS NULL OR ORDERID = :OrderId)
                  AND (:Status IS NULL OR STATUS = :Status)
                ORDER BY PAYTIME DESC
                FETCH FIRST 500 ROWS ONLY";
            var result = await _uow.Connection.QueryAsync<GroupC_FinPaymentRecord>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime,
                OrderId = string.IsNullOrWhiteSpace(orderId) ? null : orderId.Trim(),
                Status = string.IsNullOrWhiteSpace(status) ? null : status
            }, transaction);
            return result.ToList();
        }


    }


}
