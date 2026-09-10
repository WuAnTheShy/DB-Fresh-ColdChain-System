using Dapper;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public class RefundRepository : IRefundRepository
    {
        // FIN_REFUND.REMARK 列宽（迁移时已扩到 200），
        // 追加审核意见时必须按此长度截断，否则 Oracle 报 ORA-12899: value too large
        private const int RemarkMaxLength = 200;

        // 批量 IN 查询的订单号上限（订单列表一页最多 50 单，此处留足余量并防 IN 列表过长）
        private const int MaxOrderIdFilterCount = 500;

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

        // 批量查询多个订单的退款申请记录：参数化拼 IN 列表，避免逐单查询造成 N 次往返
        public async Task<List<FinRefund>> GetByOrderIdsAsync(IReadOnlyCollection<string> orderIds,
            IDbTransaction? transaction = null)
        {
            var ids = orderIds
                .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
                .Select(orderId => orderId.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(MaxOrderIdFilterCount)
                .ToList();
            if (ids.Count == 0)
            {
                return new List<FinRefund>();
            }

            var parameters = new DynamicParameters();
            var placeholders = new List<string>(ids.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                var name = $"OrderId{index}";
                placeholders.Add($":{name}");
                parameters.Add(name, ids[index]);
            }

            var sql = $@"
            SELECT * FROM FIN_REFUND
            WHERE ORDERID IN ({string.Join(", ", placeholders)})
            ORDER BY APPLYTIME DESC";
            var result = await _uow.Connection.QueryAsync<FinRefund>(sql, parameters, transaction);
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

        // 带旧状态条件的审核状态更新（乐观锁）；Oracle 中 || NULL 等价于拼接空串，审核意见可空
        public async Task<bool> TryUpdateStatusAsync(string refundId, string expectedStatus, string newStatus,
            string? auditorId, string? auditRemark, IDbTransaction? transaction = null)
        {
            // 审核意见追加到原备注之后：先按上限给原备注让位，再拼上审核意见，
            // 这样总长度恒不超过 RemarkMaxLength（REMARK 列宽），且审核意见本身不会被截断。
            var sql = $@"
            UPDATE FIN_REFUND
            SET STATUS = :NewStatus,
                AUDITTIME = SYSDATE,
                AUDITORID = :AuditorId,
                REMARK = SUBSTR(REMARK, 1, {RemarkMaxLength} - NVL(LENGTH(:RemarkSuffix), 0)) || :RemarkSuffix
            WHERE REFUNDID = :RefundId AND STATUS = :ExpectedStatus";
            var remarkSuffix = string.IsNullOrEmpty(auditRemark) ? null : $" [审核意见: {auditRemark}]";
            // 审核意见本身超长时截断，保证「让位长度」不为负、拼接结果不超列宽
            if (remarkSuffix is { Length: > RemarkMaxLength })
                remarkSuffix = remarkSuffix[..RemarkMaxLength];
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
