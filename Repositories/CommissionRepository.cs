using Dapper;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public class CommissionRepository : ICommissionRepository
    {
        private readonly IUnitOfWork _uow;

        public CommissionRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<string> InsertAsync(CommissionRecord record, IDbTransaction? transaction = null)
        {
            record.RecordId = "COM_" + Guid.NewGuid().ToString("N");
            var sql = @"
            INSERT INTO FIN_PROCOMRECORDS (
                RECORDID, PROMOTERID, ORDERID, FINALAMOUNT,
                COMMBASEAMOUNT, COMMBONUSAMOUNT, TOTALCOMMISSION,
                SIGNDATE,STATUS,EXPECTEDSETTLEDATE,REFUNDEDAMOUNT
            ) VALUES (
                :RecordId, :PromoterId, :OrderId, :FinalAmount,
                :CommBaseAmount, :CommBonusAmount, :TotalCommission,
                :SignDate, :Status,:ExpectedSettleDate,:RefundedAmount
            )";
            await _uow.Connection.ExecuteAsync(sql, record, transaction);
            return record.RecordId;
        }

        public async Task<bool> UpdateStatusAsync(string recordId, string newStatus, IDbTransaction? transaction = null)
        {
            var sql = @"UPDATE FIN_PROCOMRECORDS SET STATUS = :NewStatus WHERE RECORDID = :RecordId";
            return await _uow.Connection.ExecuteAsync(sql, new { RecordId = recordId, NewStatus = newStatus }, transaction) > 0;
        }
        public async Task<bool> UpdateRefundedAmountAsync(string recordId, decimal deltaAmount, IDbTransaction? transaction = null)
        {
            var sql = @"UPDATE FIN_PROCOMRECORDS SET REFUNDEDAMOUNT -= :DeltaAmount WHERE RECORDID = :RecordId";
            return await _uow.Connection.ExecuteAsync(sql, new { RecordId = recordId, DeltaAmount = deltaAmount }, transaction) > 0;
        }
        public async Task<CommissionRecord?> GetByOrderIdAsync(string orderId, IDbTransaction? transaction = null)
        {
            var sql = "SELECT * FROM FIN_PROCOMRECORDS WHERE ORDERID = :OrderId";
            return await _uow.Connection.QueryFirstOrDefaultAsync<CommissionRecord>(sql, new { OrderId = orderId }, transaction);
        }

        public List<CommissionRecord> GetByPromoterId(string promoterId, string? statusFilter = null)
        {
            var sql = @"SELECT * FROM FIN_PROCOMRECORDS WHERE PROMOTERID = :PromoterId"
                      + (string.IsNullOrEmpty(statusFilter) ? "" : " AND STATUS = :Status");
            var result = _uow.Connection.Query<CommissionRecord>(sql, new { PromoterId = promoterId, Status = statusFilter });
            return result.ToList();
        }
    }
}