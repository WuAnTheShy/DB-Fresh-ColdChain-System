using Dapper;
using DBFreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data.Common;
namespace DBFreshColdChain.Repositories
{
    public class PaymentRepository
    {
        private readonly IUnitOfWork _uow;  // 注入工作单元

        public PaymentRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // 供所有 Service 调用的执行 SQL 方法
        public void GroupC_AddPaymentRecord(GroupC_FinPaymentRecord finPaymentRecord)
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

            _uow.Connection.Execute(sql, finPaymentRecord);
        }


    }


}
