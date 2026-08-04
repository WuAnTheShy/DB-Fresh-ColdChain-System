using Dapper;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Configuration;
using DBFreshColdChain.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
namespace DBFreshColdChain.Repositories
{   
    public class WithdrawalRepository
    {

        private readonly IUnitOfWork _uow;  // 注入工作单元

        public WithdrawalRepository(IUnitOfWork uow)
        {
            _uow = uow;
        }
        // ========== 提现相关新增方法 ==========

        /// <summary>
        /// 检查团长是否有正在审核中的提现申请（Pending 或 Approved）
        /// </summary>
        public bool GroupC_HasPendingWithdrawal(string promoterId)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM FIN_WITHDRAWALRECORDS 
                WHERE PROMOTERID = :PromoterId 
                AND AUDITSTATUS IN ('Pending', 'Approved')";

            int count = _uow.Connection.ExecuteScalar<int>(sql, new { PromoterId = promoterId });
            return count > 0;
        }

        /// <summary>
        /// 插入提现记录
        /// </summary>
        public bool GroupC_InsertWithdrawalRecord(GroupC_FinWithdrawalRecord record)
        {
            string sql = @"
                INSERT INTO FIN_WITHDRAWALRECORDS (
                    WITHDRAWALID,
                    PROMOTERID,
                    APPLYAMOUNT,
                    ACCOUNTINFO,
                    APPLYTIME,
                    AUDITSTATUS,
                    AUDITORUSERID,
                    AUDITTIME,
                    REJECTREASON,
                    TRANSFERTIME,
                    REMARK
                ) VALUES (
                    :WithdrawalId,
                    :PromoterId,
                    :ApplyAmount,
                    :AccountInfo,
                    :ApplyTime,
                    :AuditStatus,
                    :AuditorUserId,
                    :AuditTime,
                    :RejectReason,
                    :TransferTime,
                    :Remark
                )";

            int rows = _uow.Connection.Execute(sql, record);
            return rows > 0;
        }

        /// <summary>
        /// 更新提现记录状态（审核通过/驳回）
        /// </summary>
        public bool GroupC_UpdateWithdrawalStatus(
            string withdrawalId,
            string status,
            string auditorUserId,
            DateTime? auditTime,
            DateTime? transferTime,
            string? rejectReason = null)
        {
            string sql = @"
                UPDATE FIN_WITHDRAWALRECORDS
                SET AUDITSTATUS = :Status,
                    AUDITORUSERID = :AuditorUserId,
                    AUDITTIME = :AuditTime,
                    TRANSFERTIME = :TransferTime,
                    REJECTREASON = :RejectReason
                WHERE WITHDRAWALID = :WithdrawalId";

            int rows = _uow.Connection.Execute(sql, new
            {
                WithdrawalId = withdrawalId,
                Status = status,
                AuditorUserId = auditorUserId,
                AuditTime = auditTime,
                TransferTime = transferTime,
                RejectReason = rejectReason
            });
            return rows > 0;
        }

        /// <summary>
        /// 获取提现记录（用于审核时获取申请金额等）
        /// </summary>
        public GroupC_FinWithdrawalRecord? GroupC_GetWithdrawalRecord(string withdrawalId)
        {
            string sql = @"
                SELECT 
                    WITHDRAWALID as WithdrawalId,
                    PROMOTERID as PromoterId,
                    APPLYAMOUNT as ApplyAmount,
                    ACCOUNTINFO as AccountInfo,
                    APPLYTIME as ApplyTime,
                    AUDITSTATUS as AuditStatus,
                    AUDITORUSERID as AuditorUserId,
                    AUDITTIME as AuditTime,
                    REJECTREASON as RejectReason,
                    TRANSFERTIME as TransferTime,
                    REMARK as Remark
                FROM FIN_WITHDRAWALRECORDS
                WHERE WITHDRAWALID = :WithdrawalId";

            return _uow.Connection.QueryFirstOrDefault<GroupC_FinWithdrawalRecord>(sql, new { WithdrawalId = withdrawalId });
        }

        /// <summary>
        /// 更新团长的冻结金额（增加或减少）
        /// </summary>
        public void GroupC_UpdatePromoterFrozenAmount(string promoterId, decimal delta)
        {
            string sql = @"
                UPDATE CRM_PROMOTERS
                SET FROZENAMOUNT = FROZENAMOUNT + :Delta
                WHERE PROMOTERID = :PromoterId";

            _uow.Connection.Execute(sql, new { PromoterId = promoterId, Delta = delta });
        }
    }
}


