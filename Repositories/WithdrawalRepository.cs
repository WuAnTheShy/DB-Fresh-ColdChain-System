using Dapper;
using FreshColdChain.Repositories;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public class WithdrawalRepository : IWithdrawalRepository
    {
        private readonly IUnitOfWork _uow;

        public WithdrawalRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // 检查团长是否有正在审核中的提现申请（仅 Pending；Approved 表示已打款完成，不阻止再次提现）
        public async Task<bool> GroupC_HasPendingWithdrawalAsync(
            string promoterId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM FIN_WITHDRAWALRECORDS 
                WHERE PROMOTERID = :PromoterId 
                AND AUDITSTATUS = 'Pending'";

            int count = await _uow.Connection.ExecuteScalarAsync<int>(
                sql,
                new { PromoterId = promoterId },
                transaction);
            return count > 0;
        }

        // 插入提现记录
        public async Task<bool> GroupC_InsertWithdrawalRecordAsync(
            GroupC_FinWithdrawalRecord record,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                INSERT INTO FIN_WITHDRAWALRECORDS (
                    WITHDRAWALID,
                    PROMOTERID,
                    APPLYAMOUNT,
                    ACCOUNTPLATFORM,
                    ACCOUNTNO,
                    APPLYTIME,
                    AUDITSTATUS,
                    AUDITORUSERID,
                    AUDITTIME,
                    REJECTREASON,
                    TRANSFERTIME
                ) VALUES (
                    :WithdrawalId,
                    :PromoterId,
                    :ApplyAmount,
                    :AccountPlatform,
                    :AccountNo,
                    :ApplyTime,
                    :AuditStatus,
                    :AuditorUserId,
                    :AuditTime,
                    :RejectReason,
                    :TransferTime
                )";

            int rows = await _uow.Connection.ExecuteAsync(sql, record, transaction);
            return rows > 0;
        }

        // 更新提现记录状态（审核通过/驳回）
        public async Task<bool> GroupC_UpdateWithdrawalStatusAsync(
            string withdrawalId,
            string status,
            string auditorUserId,
            DateTime? auditTime,
            DateTime? transferTime,
            string? rejectReason = null,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                UPDATE FIN_WITHDRAWALRECORDS
                SET AUDITSTATUS = :Status,
                    AUDITORUSERID = :AuditorUserId,
                    AUDITTIME = :AuditTime,
                    TRANSFERTIME = :TransferTime,
                    REJECTREASON = :RejectReason
                WHERE WITHDRAWALID = :WithdrawalId";

            int rows = await _uow.Connection.ExecuteAsync(sql, new
            {
                WithdrawalId = withdrawalId,
                Status = status,
                AuditorUserId = auditorUserId,
                AuditTime = auditTime,
                TransferTime = transferTime,
                RejectReason = rejectReason
            }, transaction);
            return rows > 0;
        }

        // 获取提现记录（用于审核时获取申请金额等）
        public async Task<GroupC_FinWithdrawalRecord?> GroupC_GetWithdrawalRecordAsync(
            string withdrawalId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT 
                    WITHDRAWALID as WithdrawalId,
                    PROMOTERID as PromoterId,
                    APPLYAMOUNT as ApplyAmount,
                    ACCOUNTPLATFORM as AccountPlatform,
                    ACCOUNTNO as AccountNo,
                    APPLYTIME as ApplyTime,
                    AUDITSTATUS as AuditStatus,
                    AUDITORUSERID as AuditorUserId,
                    AUDITTIME as AuditTime,
                    REJECTREASON as RejectReason,
                    TRANSFERTIME as TransferTime
                FROM FIN_WITHDRAWALRECORDS
                WHERE WITHDRAWALID = :WithdrawalId";

            return await _uow.Connection.QueryFirstOrDefaultAsync<GroupC_FinWithdrawalRecord>(
                sql,
                new { WithdrawalId = withdrawalId },
                transaction);
        }

        // 按团长查询全部提现记录（按申请时间倒序，团长端提现记录列表用）
        public async Task<List<GroupC_FinWithdrawalRecord>> GroupC_GetWithdrawalRecordsByPromoterAsync(
            string promoterId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT 
                    WITHDRAWALID as WithdrawalId,
                    PROMOTERID as PromoterId,
                    APPLYAMOUNT as ApplyAmount,
                    ACCOUNTPLATFORM as AccountPlatform,
                    ACCOUNTNO as AccountNo,
                    APPLYTIME as ApplyTime,
                    AUDITSTATUS as AuditStatus,
                    AUDITORUSERID as AuditorUserId,
                    AUDITTIME as AuditTime,
                    REJECTREASON as RejectReason,
                    TRANSFERTIME as TransferTime
                FROM FIN_WITHDRAWALRECORDS
                WHERE PROMOTERID = :PromoterId
                ORDER BY APPLYTIME DESC";

            var result = await _uow.Connection.QueryAsync<GroupC_FinWithdrawalRecord>(
                sql,
                new { PromoterId = promoterId },
                transaction);
            return result.ToList();
        }

        // 更新团长的冻结金额（增加或减少）
        public async Task GroupC_UpdatePromoterFrozenAmountAsync(
            string promoterId,
            decimal delta,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET FROZENAMOUNT = NVL(FROZENAMOUNT, 0) + :Delta
                WHERE PROMOTERID = :PromoterId";

            await _uow.Connection.ExecuteAsync(sql, new { PromoterId = promoterId, Delta = delta }, transaction);
        }

        public async Task<List<GroupC_FinWithdrawalRecord>> GroupC_GetPendingWithdrawalsAsync(
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        {
            const string sql = @"
            SELECT 
                WITHDRAWALID as WithdrawalId,
                PROMOTERID as PromoterId,
                APPLYAMOUNT as ApplyAmount,
                ACCOUNTPLATFORM as AccountPlatform,
                ACCOUNTNO as AccountNo,
                APPLYTIME as ApplyTime,
                AUDITSTATUS as AuditStatus,
                AUDITORUSERID as AuditorUserId,
                AUDITTIME as AuditTime,
                REJECTREASON as RejectReason,
                TRANSFERTIME as TransferTime
            FROM FIN_WITHDRAWALRECORDS
            WHERE AUDITSTATUS = 'Pending'";

            var result = await _uow.Connection.QueryAsync<GroupC_FinWithdrawalRecord>(sql,transaction: transaction);
            return result.ToList();
        }



    }
}