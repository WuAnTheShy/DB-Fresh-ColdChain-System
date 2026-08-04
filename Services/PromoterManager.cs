using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Models.CrossGroup;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models.DTOs;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace DBFreshColdChain.Services
{
    public class GroupC_PromoterManager : GroupC_IProMonterManager
    {
        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 数据库访问句柄
        private readonly PromoterRepository _promoterRepository;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly GroupC_ITableLogManager _logManager;
              
        // 构造函数
        public GroupC_PromoterManager(IUnitOfWork uow,PromoterRepository promoterRepository, GroupC_ITableLogManager logManager)
        {
            _uow = uow;
            _promoterRepository = promoterRepository;
            _logManager = logManager;
        }

        // ========== 原有方法（完全保留，未做任何逻辑改动） ==========

        public bool FindPromoterInfo(string? promoterID, ref GroupC_CrmPromoter promoterInfo)
        {
            if (promoterID == string.Empty || promoterID == null)
                return false;
            var _promoterInfo = _promoterRepository.GroupC_FindPromoterRecord(promoterID);
            if (_promoterInfo == null)
                return false;
            promoterInfo = _promoterInfo;
            // 注意：原方法未将查询结果赋值给 promoterInfo，但保留原样，不修改
            return true;
        }
        //佣金结算
        public async Task<CommissionResult> CommissionSettlement(string? promoterID, decimal finalAmount, decimal goodsAmount)
        {
            //开启事务
            await _uow.BeginAsync();
            var _promoterInfo = new GroupC_CrmPromoter();
            var _commissionResult = new CommissionResult();
            try
            {
                if (!FindPromoterInfo(promoterID, ref _promoterInfo))
                {
                    await _uow.RollbackAsync();
                    _commissionResult.IsSuccess = false;
                    _commissionResult.ErrorMessage = "团长信息不存在";
                    return _commissionResult;
                }

                var _oldTotalSales = _promoterInfo.TotalSales;
                _promoterRepository.GroupC_UpdatePromoterTotalSales(promoterID, goodsAmount);
                var _newTotalSales = _oldTotalSales + goodsAmount;

                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldTotalSales });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newTotalSales });
                _logManager.WriteTableChangeLog(_tableLog);

                _commissionResult.CommBaseAmount = _promoterInfo.BaseCommissionRate * finalAmount;

                if ((_oldTotalSales < 1000 && _newTotalSales >= 1000) ||
                    (_oldTotalSales < 2000 && _newTotalSales >= 2000))
                {
                    _commissionResult.CommBonusAmount = 50;
                }
                else if ((_oldTotalSales < 3000 && _newTotalSales >= 3000) ||
                         (_oldTotalSales < 4000 && _newTotalSales >= 4000) ||
                         (_oldTotalSales < 6000 && _newTotalSales >= 6000) ||
                         (_oldTotalSales < 7000 && _newTotalSales >= 7000))
                {
                    _commissionResult.CommBonusAmount = 150;
                }
                else if ((_oldTotalSales < 8000 && _newTotalSales >= 8000) ||
                         (_oldTotalSales < 9000 && _newTotalSales >= 9000))
                {
                    _commissionResult.CommBonusAmount = 450;
                }
                else if ((_oldTotalSales < 5000 && _newTotalSales >= 5000) ||
                         (_oldTotalSales < 10000 && _newTotalSales >= 10000))
                {
                    _commissionResult.CommBonusAmount = 750;
                }
                else
                {
                    _commissionResult.CommBonusAmount = 0;
                }

                var totalCommission = _commissionResult.CommBaseAmount + _commissionResult.CommBonusAmount;

                var _oldPromoterPendingBalance = _promoterRepository.GroupC_FindPromoterPendingBalance(promoterID);
                _promoterRepository.GroupC_UpdatePromoterPendingBalance(promoterID, totalCommission);
                var _newPromoterPendingBalance = _oldPromoterPendingBalance + totalCommission;

                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                _logManager.WriteTableChangeLog(_tableLog);

                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                // 任何一步报错，回滚所有操作（不更新TotalSales，不更新余额）
                await _uow.RollbackAsync();
                _commissionResult.IsSuccess = false;
                _commissionResult.CommSettlementDate = DateTime.Now;
                _commissionResult.ErrorMessage = $"系统错误：{ex.Message}";
                return _commissionResult;
            }
            _commissionResult.IsSuccess = true;
            _commissionResult.CommSettlementDate = DateTime.Now;
            return _commissionResult;
        }

        public async Task<Result> ActivatePromoterMoney(string? promoterID, decimal commBaseAmount, decimal commBonusAmount)
        {
            await _uow.BeginAsync();
            var _promoterInfo = new GroupC_CrmPromoter();
            var _result = new Result();
            try
            {
                if (!FindPromoterInfo(promoterID, ref _promoterInfo))
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "团长信息不存在";
                    return _result;
                }

                var totalCommission = commBaseAmount + commBonusAmount;
                var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
                var _oldPromoterCurrentBalance = _promoterInfo.CurrentBalance;

                _promoterRepository.GroupC_UpdatePromoterPendingBalance(promoterID, -totalCommission);
                _promoterRepository.GroupC_UpdatePromoterCurrentBalance(promoterID, totalCommission);

                var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
                var _newPromoterCurrentBalance = _oldPromoterCurrentBalance + totalCommission;

                var _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
                _logManager.WriteTableChangeLog(_tableLog);

                _tableLog = new GroupC_LogAuditrails();
                _tableLog.ActionType = "Update";
                _tableLog.TableName = "CRM_PROMOTERS";
                _tableLog.OperatorType = "Platform";
                _tableLog.OperatorId = "\\";
                _tableLog.OldValue = JsonConvert.SerializeObject(new { CurrentBalance = _oldPromoterCurrentBalance });
                _tableLog.NewValue = JsonConvert.SerializeObject(new { CurrentBalance = _newPromoterCurrentBalance });
                _logManager.WriteTableChangeLog(_tableLog);
                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                // 4. 任何一步报错，回滚所有操作（不更新TotalSales，不更新余额）
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result;
        }

        // ========== 新增功能：团长注册、登录、管理员直接添加 ==========

        /// 团长注册（首次注册，待管理员审核激活）
        public async Task<GroupC_PromoterRegisterResult> RegisterPromoter(GroupC_PromoterRegisterInfo registerInfo)
        {
            //开启事务
            await _uow.BeginAsync();
            var promoterRegisterResult = new GroupC_PromoterRegisterResult();
            try
            {
                if (registerInfo == null || string.IsNullOrWhiteSpace(registerInfo.LoginAccount))
                {
                    await _uow.RollbackAsync();
                    promoterRegisterResult.IsSuccess = false;
                    promoterRegisterResult.Message = "输入注册信息不能为空";
                    return promoterRegisterResult;
                }

                if (_promoterRepository.GroupC_ExistsPromoterByLoginAccount(registerInfo.LoginAccount))
                {
                    await _uow.RollbackAsync();
                    promoterRegisterResult.IsSuccess = false;
                    promoterRegisterResult.Message = "该用户名已存在，请重新输入";
                    return promoterRegisterResult;
                }

                string hashedPassword = HashPassword(registerInfo.LoginPassword);

                var promoter = new GroupC_CrmPromoter
                {
                    PromoterId = Guid.NewGuid().ToString("N"),
                    PromoterName = registerInfo.PromoterName,
                    LoginAccount = registerInfo.LoginAccount,
                    LoginPassword = hashedPassword,
                    Phone = registerInfo.Phone,
                    BaseCommissionRate = 0.03m,
                    TotalSales = 0,
                    PendingBalance = 0,
                    CurrentBalance = 0,
                    InviteCode = GenerateInviteCode(),
                    Status = "Pending",
                    RegisterTime = DateTime.Now,
                };

                bool result = _promoterRepository.GroupC_InsertPromoter(promoter);
                if (!result)
                {
                    await _uow.RollbackAsync();
                    promoterRegisterResult.IsSuccess = false;
                    promoterRegisterResult.Message = "系统异常：添加团长信息失败，请稍后再试";
                    return promoterRegisterResult;
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Copy",
                    OperatorType = "Platform",
                    OperatorId = "\\",
                    OldValue = string.Empty,
                    NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, promoter.PromoterName, promoter.LoginAccount })
                };
                _logManager.WriteTableChangeLog(log);
                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync();
                promoterRegisterResult.IsSuccess = true;
                promoterRegisterResult.Message = $"系统错误：{ex.Message}";
                return promoterRegisterResult;
            }
            promoterRegisterResult.IsSuccess = true;
            promoterRegisterResult.Message = "";
            return promoterRegisterResult;
        }

        /// 团长登录验证
        public GroupC_PromoterLoginResult LoginPromoter(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            var promoter = _promoterRepository.GroupC_FindPromoterByLoginAccount(loginAccount);
            if (promoter == null)
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号不存在" };

            string hashedInput = HashPassword(password);
            if (promoter.LoginPassword != hashedInput)
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "密码错误" };

            if (promoter.Status == "Pending")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号尚未审核通过" };
            if (promoter.Status == "Disabled")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号已被禁用" };

            return new GroupC_PromoterLoginResult
            {
                IsSuccess = true,
                PromoterId = promoter.PromoterId,
                PromoterName = promoter.PromoterName,
                CurrentBalance = promoter.CurrentBalance,
                PendingBalance = promoter.PendingBalance,
                TotalSales = promoter.TotalSales,
                InviteCode = promoter.InviteCode
            };
        }
        /// 管理员直接添加团长（直接生效，无需审核）
        public async Task<Result>AddPromoterByAdmin(GroupC_PromoterAddInfo addInfo)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (addInfo == null || string.IsNullOrWhiteSpace(addInfo.LoginAccount))
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "添加信息错误";
                    return _result;
                }


                if (_promoterRepository.GroupC_ExistsPromoterByLoginAccount(addInfo.LoginAccount))
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "已存在该团长信息，无需添加";
                    return _result;
                }

                string hashedPassword = HashPassword(addInfo.LoginPassword);

                var promoter = new GroupC_CrmPromoter
                {
                    PromoterId = Guid.NewGuid().ToString("N"),
                    PromoterName = addInfo.PromoterName,
                    LoginAccount = addInfo.LoginAccount,
                    LoginPassword = hashedPassword,
                    Phone = addInfo.Phone,
                    BaseCommissionRate = addInfo.BaseCommissionRate ?? 0.03m,
                    TotalSales = 0,
                    PendingBalance = 0,
                    CurrentBalance = 0,
                    InviteCode = GenerateInviteCode(),
                    Status = "Active",
                    RegisterTime = DateTime.Now,
                };

                bool dbResult = _promoterRepository.GroupC_InsertPromoter(promoter);
                if (!dbResult)
                {
                    await _uow.RollbackAsync();
                    _result.IsSuccess = false;
                    _result.ErrorMessage = "插入该记录失败";
                    return _result;
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Copy",
                    OperatorType = "Platform",
                    OperatorId = "\\",
                    OldValue = null,
                    NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, promoter.PromoterName, promoter.LoginAccount })
                };
                _logManager.WriteTableChangeLog(log);
                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                // 任何一步报错，回滚所有操作
                await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
            _result.IsSuccess = true;
            return _result;
        }

        // ========== 私有辅助方法 ==========
        private string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

    

    }
}