using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Interfaces;
using Newtonsoft.Json;
using System;

namespace DBFreshColdChain.Services
{
    public class WithdrawalManager
    {
        private readonly DbHelper _dbHelper;
        private readonly GroupC_ITableLogManager _logManager;

        public WithdrawalManager(DbHelper dbHelper, GroupC_ITableLogManager logManager)
        {
            _dbHelper = dbHelper;
            _logManager = logManager;
        }

        /// <summary>
        /// 发起提现申请
        /// </summary>
        /// <param name="operatorId">操作者ID（通常为平台管理员）</param>
        /// <param name="request">提现请求</param>
        /// <returns>(是否成功, 错误信息)</returns>
        public (bool Success, string Message) ApplyWithdrawal(string operatorId, WithdrawalRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PromoterId))
                return (false, "提现请求无效");

            // 1. 查找团长
            var promoter = _dbHelper.GroupC_FindPromoterRecord(request.PromoterId);
            if (promoter == null)
                return (false, "团长不存在");

            // 2. 防重检验：是否有正在审核的申请（Pending 或 Approved）
            if (_dbHelper.GroupC_HasPendingWithdrawal(request.PromoterId))
                return (false, "该团长已有正在审核或已通过的提现申请，请等待完成");

            // 3. 余额充足校验
            if (promoter.CurrentBalance < request.ApplyAmount)
                return (false, $"可用余额不足，当前可用余额：{promoter.CurrentBalance}");

            // 4. 扣减可用余额，增加冻结金额
            _dbHelper.GroupC_UpdatePromoterCurrentBalance(request.PromoterId, -request.ApplyAmount);
            _dbHelper.GroupC_UpdatePromoterFrozenAmount(request.PromoterId, request.ApplyAmount);

            // 5. 记录提现申请
            var record = new FinWithdrawalRecord
            {
                WithdrawalId = Guid.NewGuid().ToString("N"),
                PromoterId = request.PromoterId,
                ApplyAmount = request.ApplyAmount,
                AccountInfo = request.AccountInfo,
                ApplyTime = DateTime.Now,
                AuditStatus = "Pending",
                AuditorUserId = string.Empty,
                AuditTime = null,
                RejectReason = string.Empty,
                TransferTime = null,
                Remark = string.Empty
            };

            bool insertOk = _dbHelper.GroupC_InsertWithdrawalRecord(record);
            if (!insertOk)
            {
                // 回滚余额操作（简单处理，此处抛异常或返回失败）
                _dbHelper.GroupC_UpdatePromoterCurrentBalance(request.PromoterId, request.ApplyAmount);
                _dbHelper.GroupC_UpdatePromoterFrozenAmount(request.PromoterId, -request.ApplyAmount);
                return (false, "创建提现记录失败");
            }

            // 6. 记录日志（团长表变更：CurrentBalance 和 FrozenAmount）
            var log = new Log_Auditrails
            {
                TableName = "CRM_PROMOTERS",
                ActionType = "Update",
                OperatorType = "Platform",
                OperatorId = operatorId,
                OldValue = JsonConvert.SerializeObject(new { CurrentBalance = promoter.CurrentBalance, FrozenAmount = promoter.FrozenAmount }),
                NewValue = JsonConvert.SerializeObject(new
                {
                    CurrentBalance = promoter.CurrentBalance - request.ApplyAmount,
                    FrozenAmount = promoter.FrozenAmount + request.ApplyAmount
                }),
                RecordId = request.PromoterId
            };
            _logManager.WriteTableChangeLog(log);

            // 记录提现表日志
            var log2 = new Log_Auditrails
            {
                TableName = "FIN_WITHDRAWALRECORDS",
                ActionType = "Copy",
                OperatorType = "Platform",
                OperatorId = operatorId,
                OldValue = null,
                NewValue = JsonConvert.SerializeObject(new { record.WithdrawalId, record.PromoterId, record.ApplyAmount }),
                RecordId = record.WithdrawalId
            };
            _logManager.WriteTableChangeLog(log2);

            return (true, "提现申请已提交，等待审核");
        }

        /// <summary>
        /// 审核通过提现申请（打款）
        /// </summary>
        public (bool Success, string Message) ApproveWithdrawal(string operatorId, WithdrawApproved approved)
        {
            if (approved == null || string.IsNullOrWhiteSpace(approved.WithdrawalId))
                return (false, "审核请求无效");

            // 1. 获取提现记录
            var record = _dbHelper.GroupC_GetWithdrawalRecord(approved.WithdrawalId);
            if (record == null)
                return (false, "提现记录不存在");
            if (record.AuditStatus != "Pending")
                return (false, $"当前状态为 {record.AuditStatus}，无法审核");

            // 2. 更新提现记录状态为 Approved，记录审核人和时间、打款时间（TransferTime 与 AuditTime 一致）
            bool updateOk = _dbHelper.GroupC_UpdateWithdrawalStatus(
                approved.WithdrawalId,
                "Approved",
                approved.UserId,
                approved.AuditTime,
                approved.AuditTime,   // TransferTime 也设为审核时间
                null                  // 驳回原因无需
            );
            if (!updateOk)
                return (false, "更新提现记录失败");

            // 3. 减少团长冻结金额（该笔金额已在申请时冻结，现正式扣除）
            _dbHelper.GroupC_UpdatePromoterFrozenAmount(record.PromoterId, -record.ApplyAmount);

            // 4. 记录日志（团长表 FrozenAmount 变更）
            var promoter = _dbHelper.GroupC_FindPromoterRecord(record.PromoterId);
            if (promoter != null)
            {
                var log = new Log_Auditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Platform",
                    OperatorId = operatorId,
                    OldValue = JsonConvert.SerializeObject(new { FrozenAmount = promoter.FrozenAmount + record.ApplyAmount }),
                    NewValue = JsonConvert.SerializeObject(new { FrozenAmount = promoter.FrozenAmount }),
                    RecordId = record.PromoterId
                };
                _logManager.WriteTableChangeLog(log);
            }

            // 记录提现表更新日志
            var log2 = new Log_Auditrails
            {
                TableName = "FIN_WITHDRAWALRECORDS",
                ActionType = "Update",
                OperatorType = "Platform",
                OperatorId = operatorId,
                OldValue = JsonConvert.SerializeObject(new { AuditStatus = "Pending" }),
                NewValue = JsonConvert.SerializeObject(new { AuditStatus = "Approved", AuditorUserId = approved.UserId, AuditTime = approved.AuditTime, TransferTime = approved.AuditTime }),
                RecordId = approved.WithdrawalId
            };
            _logManager.WriteTableChangeLog(log2);

            return (true, "提现审核通过，已打款");
        }

        /// <summary>
        /// 驳回提现申请
        /// </summary>
        public (bool Success, string Message) RejectWithdrawal(string operatorId, WithdrawRejected rejected)
        {
            if (rejected == null || string.IsNullOrWhiteSpace(rejected.WithdrawalId))
                return (false, "驳回请求无效");

            // 1. 获取提现记录
            var record = _dbHelper.GroupC_GetWithdrawalRecord(rejected.WithdrawalId);
            if (record == null)
                return (false, "提现记录不存在");
            if (record.AuditStatus != "Pending")
                return (false, $"当前状态为 {record.AuditStatus}，无法驳回");

            // 2. 更新状态为 Rejected，记录审核人、时间、驳回原因
            bool updateOk = _dbHelper.GroupC_UpdateWithdrawalStatus(
                rejected.WithdrawalId,
                "Rejected",
                rejected.UserId,
                rejected.AuditTime,
                null,                  // TransferTime 留空
                rejected.RejectReason
            );
            if (!updateOk)
                return (false, "更新提现记录失败");

            // 3. 解冻金额：减少冻结金额，同时加回可用余额
            _dbHelper.GroupC_UpdatePromoterFrozenAmount(record.PromoterId, -record.ApplyAmount);
            _dbHelper.GroupC_UpdatePromoterCurrentBalance(record.PromoterId, record.ApplyAmount);

            // 4. 记录日志（团长表 FrozenAmount 和 CurrentBalance 变更）
            var promoter = _dbHelper.GroupC_FindPromoterRecord(record.PromoterId);
            if (promoter != null)
            {
                var log = new Log_Auditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Platform",
                    OperatorId = operatorId,
                    OldValue = JsonConvert.SerializeObject(new { CurrentBalance = promoter.CurrentBalance - record.ApplyAmount, FrozenAmount = promoter.FrozenAmount + record.ApplyAmount }),
                    NewValue = JsonConvert.SerializeObject(new { CurrentBalance = promoter.CurrentBalance, FrozenAmount = promoter.FrozenAmount }),
                    RecordId = record.PromoterId
                };
                _logManager.WriteTableChangeLog(log);
            }

            var log2 = new Log_Auditrails
            {
                TableName = "FIN_WITHDRAWALRECORDS",
                ActionType = "Update",
                OperatorType = "Platform",
                OperatorId = operatorId,
                OldValue = JsonConvert.SerializeObject(new { AuditStatus = "Pending" }),
                NewValue = JsonConvert.SerializeObject(new { AuditStatus = "Rejected", AuditorUserId = rejected.UserId, AuditTime = rejected.AuditTime, RejectReason = rejected.RejectReason }),
                RecordId = rejected.WithdrawalId
            };
            _logManager.WriteTableChangeLog(log2);

            return (true, "提现申请已驳回");
        }
        //DTO类

            /// 提现申请请求
        public class WithdrawalRequest
        {
            public string PromoterId { get; set; } = string.Empty;
            public string AccountInfo { get; set; } = string.Empty;   // 收款账户信息
            public decimal ApplyAmount { get; set; }
        }
        /// 提现审核通过请求
        public class WithdrawApproved
        {
            public string WithdrawalId { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;        // 审核人ID
            public DateTime AuditTime { get; set; }                   // 审核时间
        }
        /// 提现审核驳回请求
        public class WithdrawRejected
        {
            public string WithdrawalId { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string RejectReason { get; set; } = string.Empty;
            public DateTime AuditTime { get; set; }
        }
    }
}