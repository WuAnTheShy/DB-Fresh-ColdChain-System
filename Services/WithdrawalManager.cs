using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
using Newtonsoft.Json;
using System;
using System.Configuration;

namespace DBFreshColdChain.Services
{
    public class GroupC_WithdrawalManager
    {
        private readonly IUnitOfWork _uow;
        private readonly PromoterRepository _promoterRepository;
        private readonly WithdrawalRepository _withdrawalRepository;
        private readonly GroupC_ITableLogManager _logManager;

        public GroupC_WithdrawalManager(IUnitOfWork uow, WithdrawalRepository withdrawalRepository, PromoterRepository promoterRepository, GroupC_ITableLogManager logManager)
        {
            _uow = uow;
            _withdrawalRepository = withdrawalRepository;   
            _promoterRepository = promoterRepository;
            _logManager = logManager;
        }

        /// <summary>
        /// 发起提现申请
        /// </summary>
        /// <param name="operatorId">操作者ID（通常为平台管理员）</param>
        /// <param name="request">提现请求</param>
        /// <returns>(是否成功, 错误信息)</returns>
        public async Task<Result> ApplyWithdrawal(string operatorId, GroupC_WithdrawalRequest request)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.PromoterId))
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "提现请求无效";
                    return _result;
                }
                

                // 1. 查找团长
                var promoter = _promoterRepository.GroupC_FindPromoterRecord(request.PromoterId);
                if (promoter == null)
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "团长不存在";
                    return _result;
                }

                // 2. 防重检验：是否有正在审核的申请（Pending 或 Approved）
                if (_withdrawalRepository.GroupC_HasPendingWithdrawal(request.PromoterId))
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "该团长已有正在审核或已通过的提现申请，请等待完成";
                    return _result;
                }

                // 3. 余额充足校验
                if (promoter.CurrentBalance < request.ApplyAmount)
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = $"可用余额不足，当前可用余额：{promoter.CurrentBalance}";
                    return _result;
                }

                // 4. 扣减可用余额，增加冻结金额
                _promoterRepository.GroupC_UpdatePromoterCurrentBalance(request.PromoterId, -request.ApplyAmount);
                _withdrawalRepository.GroupC_UpdatePromoterFrozenAmount(request.PromoterId, request.ApplyAmount);

                // 5. 记录提现申请
                var record = new GroupC_FinWithdrawalRecord
                {
                    WithdrawalId = "Wd_" + Guid.NewGuid().ToString("N"),
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
                bool insertOk = _withdrawalRepository.GroupC_InsertWithdrawalRecord(record);
                if (!insertOk)
                {
                    throw new Exception("创建提现记录失败");
                }

                // 6. 记录日志（团长表变更：CurrentBalance 和 FrozenAmount）
                var log = new GroupC_LogAuditrails
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
                var log2 = new GroupC_LogAuditrails
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
                await _uow.CommitAsync(); 
            }
            catch (Exception ex)
            {
                // 任何步骤失败，统一回滚所有操作（余额、冻结金额、提现记录均撤销）
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result; 
        }

        /// <summary>
        /// 审核通过提现申请（打款）
        /// </summary>
        public async Task<Result> ApproveWithdrawal(string operatorId,GroupC_WithdrawApproved approved)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (approved == null || string.IsNullOrWhiteSpace(approved.WithdrawalId))
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "审核请求无效";
                    return _result;
                }

                // 1. 获取提现记录
                var record = _withdrawalRepository.GroupC_GetWithdrawalRecord(approved.WithdrawalId);
                if (record == null)
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "提现记录不存在";
                    return _result;
                }
                if (record.AuditStatus != "Pending")
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = $"当前状态为 {record.AuditStatus}，无法审核";
                    return _result;
                }

                // 2. 更新提现记录状态为 Approved，记录审核人和时间、打款时间（TransferTime 与 AuditTime 一致）
                bool updateOk = _withdrawalRepository.GroupC_UpdateWithdrawalStatus(
                    approved.WithdrawalId,
                    "Approved",
                    approved.UserId,
                    approved.AuditTime,
                    approved.AuditTime,   // TransferTime 也设为审核时间
                    null                  // 驳回原因无需
                );
                if (!updateOk)
                {
                    throw new Exception("更新提现记录失败");
                }

                // 3. 减少团长冻结金额（该笔金额已在申请时冻结，现正式扣除）
                _withdrawalRepository.GroupC_UpdatePromoterFrozenAmount(record.PromoterId, -record.ApplyAmount);

                // 4. 记录日志（团长表 FrozenAmount 变更）
                var promoter = _promoterRepository.GroupC_FindPromoterRecord(record.PromoterId);
                if (promoter != null)
                {
                    var log = new GroupC_LogAuditrails
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
                var log2 = new GroupC_LogAuditrails
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
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result;
        }

        /// <summary>
        /// 驳回提现申请
        /// </summary>
        public async Task<Result> RejectWithdrawal(string operatorId, GroupC_WithdrawRejected rejected)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (rejected == null || string.IsNullOrWhiteSpace(rejected.WithdrawalId))
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "驳回请求无效";
                    return _result;
                }

                // 1. 获取提现记录
                var record = _withdrawalRepository.GroupC_GetWithdrawalRecord(rejected.WithdrawalId);
                if (record == null)
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "提现记录不存在";
                    return _result;
                }
                if (record.AuditStatus != "Pending")
                {
                    _result.IsSuccess = false;
                    _result.ErrorMessage = $"当前状态为 {record.AuditStatus}，无法驳回"; ;
                    return _result;
                }

                // 2. 更新状态为 Rejected，记录审核人、时间、驳回原因
                bool updateOk = _withdrawalRepository.GroupC_UpdateWithdrawalStatus(
                    rejected.WithdrawalId,
                    "Rejected",
                    rejected.UserId,
                    rejected.AuditTime,
                    null,                  // TransferTime 留空
                    rejected.RejectReason
                );
                if (!updateOk)
                    throw new Exception("更新提现记录失败");

                // 3. 解冻金额：减少冻结金额，同时加回可用余额
                _withdrawalRepository.GroupC_UpdatePromoterFrozenAmount(record.PromoterId, -record.ApplyAmount);
                _promoterRepository.GroupC_UpdatePromoterCurrentBalance(record.PromoterId, record.ApplyAmount);

                // 4. 记录日志（团长表 FrozenAmount 和 CurrentBalance 变更）
                var promoter = _promoterRepository.GroupC_FindPromoterRecord(record.PromoterId);
                if (promoter != null)
                {
                    var log = new GroupC_LogAuditrails
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

                var log2 = new GroupC_LogAuditrails
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
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result;
        }
        //DTO类

           
        
        
        
    }
}