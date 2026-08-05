using Dapper;
using DBFreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;
namespace DBFreshColdChain.Repositories
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

            await _uow.Connection.ExecuteAsync(sql, finPaymentRecord, transaction);  
        }


    }


}
