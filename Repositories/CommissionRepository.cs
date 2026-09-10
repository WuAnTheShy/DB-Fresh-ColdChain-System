using Dapper;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
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

        // 带旧状态条件的原子状态更新（乐观锁），仅当当前状态为 expectedStatus 时才更新
        public async Task<bool> TryUpdateStatusAsync(string recordId, string expectedStatus, string newStatus, IDbTransaction? transaction = null)
        {
            var sql = @"UPDATE FIN_PROCOMRECORDS SET STATUS = :NewStatus
                        WHERE RECORDID = :RecordId AND STATUS = :ExpectedStatus";
            return await _uow.Connection.ExecuteAsync(sql, new { RecordId = recordId, ExpectedStatus = expectedStatus, NewStatus = newStatus }, transaction) > 0;
        }

        // 查询已过退款期（到达预计结算时间）仍未结算的佣金记录
        public async Task<List<CommissionRecord>> GetDueSettlementsAsync(DateTime now, IDbTransaction? transaction = null)
        {
            var sql = @"SELECT * FROM FIN_PROCOMRECORDS
                        WHERE STATUS = 'Pending' AND EXPECTEDSETTLEDATE <= :Now";
            var result = await _uow.Connection.QueryAsync<CommissionRecord>(sql, new { Now = now }, transaction);
            return result.ToList();
        }
        public async Task<bool> UpdateRefundedAmountAsync(string recordId, decimal deltaAmount, IDbTransaction? transaction = null)
        {
            var sql = @"UPDATE FIN_PROCOMRECORDS SET REFUNDEDAMOUNT = REFUNDEDAMOUNT + :DeltaAmount WHERE RECORDID = :RecordId";
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