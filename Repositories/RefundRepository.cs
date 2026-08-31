using Dapper;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
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
                    Remark,
                    Status)
            VALUES (:RefundId, :OrderId, :DetailId, :SupplierId,
                    :RefundAmount, :RefundQty, :LiabilityType,
                    :ApplyTime, :Remark, :Status)";
            await _uow.Connection.ExecuteAsync(sql, refund, transaction);
        }

        public async Task<FinRefund?> GetByIdAsync(string refundId, IDbTransaction? transaction = null)
        {
            const string sql = "SELECT * FROM FIN_REFUND WHERE REFUNDID = :RefundId";
            return await _uow.Connection.QueryFirstOrDefaultAsync<FinRefund>(sql, new { RefundId = refundId }, transaction);
        }

        public async Task<List<FinRefund>> GetByStatusAsync(string status, IDbTransaction? transaction = null)
        {
            const string sql = "SELECT * FROM FIN_REFUND WHERE STATUS = :Status ORDER BY APPLYTIME";
            var result = await _uow.Connection.QueryAsync<FinRefund>(sql, new { Status = status }, transaction);
            return result.ToList();
        }

        public async Task<List<FinRefund>> GetByOrderIdAsync(string orderId, IDbTransaction? transaction = null)
        {
            const string sql = "SELECT * FROM FIN_REFUND WHERE ORDERID = :OrderId ORDER BY APPLYTIME DESC";
            var result = await _uow.Connection.QueryAsync<FinRefund>(sql, new { OrderId = orderId }, transaction);
            return result.ToList();
        }

        public async Task<bool> HasPendingApplicationAsync(string orderId, IDbTransaction? transaction = null)
        {
            const string sql = "SELECT COUNT(1) FROM FIN_REFUND WHERE ORDERID = :OrderId AND STATUS = 'Pending'";
            var count = await _uow.Connection.ExecuteScalarAsync<int>(sql, new { OrderId = orderId }, transaction);
            return count > 0;
        }

        // 组合查询退款记录（管理端查询页用，结果上限 500 条）
        public async Task<List<FinRefund>> SearchAsync(DateTime? startTime, DateTime? endTime,
            string? orderId, string? status, IDbTransaction? transaction = null)
        {
            const string sql = @"
            SELECT * FROM FIN_REFUND
            WHERE (:StartTime IS NULL OR APPLYTIME >= :StartTime)
              AND (:EndTime IS NULL OR APPLYTIME < :EndTime)
              AND (:OrderId IS NULL OR ORDERID = :OrderId)
              AND (:Status IS NULL OR STATUS = :Status)
            ORDER BY APPLYTIME DESC
            FETCH FIRST 500 ROWS ONLY";
            var result = await _uow.Connection.QueryAsync<FinRefund>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime,
                OrderId = string.IsNullOrWhiteSpace(orderId) ? null : orderId.Trim(),
                Status = string.IsNullOrWhiteSpace(status) ? null : status
            }, transaction);
            return result.ToList();
        }

        /// <summary>带旧状态条件的审核状态更新（乐观锁）；Oracle 中 || NULL 等价于拼接空串，审核意见可空</summary>
        public async Task<bool> TryUpdateStatusAsync(string refundId, string expectedStatus, string newStatus,
            string? auditorId, string? auditRemark, IDbTransaction? transaction = null)
        {
            const string sql = @"
            UPDATE FIN_REFUND
            SET STATUS = :NewStatus,
                AUDITTIME = SYSDATE,
                AUDITORID = :AuditorId,
                REMARK = REMARK || :RemarkSuffix
            WHERE REFUNDID = :RefundId AND STATUS = :ExpectedStatus";
            var remarkSuffix = string.IsNullOrEmpty(auditRemark) ? null : $" [审核意见: {auditRemark}]";
            return await _uow.Connection.ExecuteAsync(sql, new
            {
                RefundId = refundId,
                ExpectedStatus = expectedStatus,
                NewStatus = newStatus,
                AuditorId = auditorId,
                RemarkSuffix = remarkSuffix
            }, transaction) > 0;
        }
    }
}
