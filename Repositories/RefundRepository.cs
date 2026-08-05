using Dapper;
using DBFreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public class RefundRepository : IRefundRepository
    {
        private readonly IUnitOfWork _uow;

        public RefundRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task InsertRefundAsync(FinRefund refund, IDbTransaction? transaction = null)
        {
            const string sql = @"
            INSERT INTO FIN_REFUND (
                    RefundId, 
                    OrderId, 
                    DetailId,
                    SupplierId,                 
                    RefundAmount, 
                    RefundQty, 
                    LiabilityType, 
                    ApplyTime, 
                    Remark)
            VALUES (:RefundId, :OrderId, :DetailId, :SupplierId, 
                    :RefundAmount, :RefundQty, :LiabilityType, 
                    :ApplyTime, :Remark)";
            await _uow.Connection.ExecuteAsync(sql, refund, transaction);
        }
    }
}
