using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Models;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data;

namespace DBFreshColdChain.Services
{
    public class WithdrawalService
    {
        private readonly IUnitOfWork _uow;
        private readonly IPromoterRepository _ipromoterRepository;
        private readonly IWithdrawalRepository _iwithdrawalRepository;
        private readonly ITableLogService _logManager;

        public WithdrawalService(IUnitOfWork uow, IWithdrawalRepository iwithdrawalRepository, IPromoterRepository ipromoterRepository, ITableLogService logManager)
        {
            _uow = uow;
            _iwithdrawalRepository = iwithdrawalRepository;   
            _ipromoterRepository = ipromoterRepository;
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
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(request.PromoterId, _uow.Transaction);
                if (promoter == null)
                {
                    throw new Exception("团长不存在");
                }

                // 2. 防重检验：是否有正在审核的申请（Pending 或 Approved）
                var exists = await _iwithdrawalRepository.GroupC_HasPendingWithdrawalAsync(request.PromoterId, _uow.Transaction);
                if (exists)
                {
                    throw new Exception("该团长已有正在审核或已通过的提现申请，请等待完成");
                }

                // 3. 余额充足校验
                if (promoter.CurrentBalance < request.ApplyAmount)
                {
                    throw new Exception($"可用余额不足，当前可用余额为 {promoter.CurrentBalance}元");

                }

                // 4. 扣减可用余额，增加冻结金额
                await _ipromoterRepository.GroupC_UpdatePromoterCurrentBalanceAsync(request.PromoterId, -request.ApplyAmount, _uow.Transaction);
                await _iwithdrawalRepository.GroupC_UpdatePromoterFrozenAmountAsync(request.PromoterId, request.ApplyAmount, _uow.Transaction);

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
                    TransferTime = null
                };
                bool insertOk = await _iwithdrawalRepository.GroupC_InsertWithdrawalRecordAsync(record, _uow.Transaction);
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
                await _logManager.WriteTableChangeLog(log);

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
                await _logManager.WriteTableChangeLog(log2);
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                // 任何步骤失败，统一回滚所有操作
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            
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
                    throw new Exception(_result.ErrorMessage = "审核请求无效");
                }

                // 1. 获取提现记录
                var record = await _iwithdrawalRepository.GroupC_GetWithdrawalRecordAsync(approved.WithdrawalId,_uow.Transaction);
                if (record == null)
                {
                    throw new Exception(_result.ErrorMessage = "提现记录不存在");
                }
                if (record.AuditStatus != "Pending")
                {
                    throw new Exception(_result.ErrorMessage = $"当前状态为 {record.AuditStatus}，无法审核");
                }

                // 2. 更新提现记录状态为 Approved，记录审核人和时间、打款时间（TransferTime 与 AuditTime 一致）
                bool updateOk = await  _iwithdrawalRepository.GroupC_UpdateWithdrawalStatusAsync(
                    approved.WithdrawalId,
                    "Approved",
                    approved.UserId,
                    approved.AuditTime,
                    approved.AuditTime,   // TransferTime 也设为审核时间
                    null,                  // 驳回原因无需
                    _uow.Transaction
                );
                if (!updateOk)
                {
                    throw new Exception("更新提现记录失败");
                }

                // 3. 减少团长冻结金额（该笔金额已在申请时冻结，现正式扣除）
                await _iwithdrawalRepository.GroupC_UpdatePromoterFrozenAmountAsync(record.PromoterId, -record.ApplyAmount,_uow.Transaction);

                // 4. 记录日志（团长表 FrozenAmount 变更）
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(record.PromoterId, _uow.Transaction);
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
                    await _logManager.WriteTableChangeLog(log);
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
                await _logManager.WriteTableChangeLog(log2);
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            
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
                    throw new Exception("驳回请求无效");
                }

                // 1. 获取提现记录
                var record = await _iwithdrawalRepository.GroupC_GetWithdrawalRecordAsync(rejected.WithdrawalId, _uow.Transaction);
                if (record == null)
                {
                    throw new Exception("提现记录不存在");
                }
                if (record.AuditStatus != "Pending")
                {
                    throw new Exception($"当前状态为 {record.AuditStatus}，无法驳回");
                }

                // 2. 更新状态为 Rejected，记录审核人、时间、驳回原因
                bool updateOk = await _iwithdrawalRepository.GroupC_UpdateWithdrawalStatusAsync(
                    rejected.WithdrawalId,
                    "Rejected",
                    rejected.UserId,
                    rejected.AuditTime,
                    null,                  // TransferTime 留空
                    rejected.RejectReason,
                    _uow.Transaction
                );
                if (!updateOk)
                    throw new Exception("更新提现记录失败");

                // 3. 解冻金额：减少冻结金额，同时加回可用余额
                await _iwithdrawalRepository.GroupC_UpdatePromoterFrozenAmountAsync(record.PromoterId, -record.ApplyAmount, _uow.Transaction);
                await _ipromoterRepository.GroupC_UpdatePromoterCurrentBalanceAsync(record.PromoterId, record.ApplyAmount, _uow.Transaction);

                // 4. 记录日志（团长表 FrozenAmount 和 CurrentBalance 变更）
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(record.PromoterId, _uow.Transaction);
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
                    await _logManager.WriteTableChangeLog(log);
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
                await _logManager.WriteTableChangeLog(log2);
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }

        }

        public async Task<List<GroupC_FinWithdrawalRecord>> GetPendingWithdrawalsAsync()
        {
            return await _iwithdrawalRepository.GroupC_GetPendingWithdrawalsAsync();
        }



    }
}