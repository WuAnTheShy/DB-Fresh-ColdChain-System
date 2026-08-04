using DBFreshColdChain.Models;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Interfaces;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace DBFreshColdChain.Services
{
    public class PromoterManager : GroupC_IProMonterManager
    {
        // Repository层句柄
        private readonly DbHelper _dbHelper;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly GroupC_ITableLogManager _logManager;

        // 构造函数
        public PromoterManager(DbHelper dbHelper, GroupC_ITableLogManager logManager)
        {
            _dbHelper = dbHelper;
            _logManager = logManager;
        }

        // ========== 原有方法（完全保留，未做任何逻辑改动） ==========

        public bool FindPromoterInfo(string? promoterID, ref CrmPromoter promoterInfo)
        {
            if (promoterID == string.Empty || promoterID == null)
                return false;
            var _promoterInfo = _dbHelper.GroupC_FindPromoterRecord(promoterID);
            if (_promoterInfo == null)
                return false;
            promoterInfo = _promoterInfo;
            // 注意：原方法未将查询结果赋值给 promoterInfo，但保留原样，不修改
            return true;
        }

        public bool CommisionSettlement(string? promoterID, decimal finalAmount, decimal goodsAmount, ref CommissionInfo commissionInfo)
        {
            CrmPromoter _promoterInfo = new CrmPromoter();
            if (!FindPromoterInfo(promoterID, ref _promoterInfo))
            {
                commissionInfo = new CommissionInfo();
                return false;
            }

            var _oldTotalSales = _promoterInfo.TotalSales;
            _dbHelper.GroupC_UpdatePromoterTotalSales(promoterID, goodsAmount);
            var _newTotalSales = _oldTotalSales + goodsAmount;

            var _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { TotalSales = _oldTotalSales });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { TotalSales = _newTotalSales });
            _logManager.WriteTableChangeLog(_tableLog);

            commissionInfo.CommBaseAmount = _promoterInfo.BaseCommissionRate * finalAmount;

            if ((_oldTotalSales < 1000 && _newTotalSales >= 1000) ||
                (_oldTotalSales < 2000 && _newTotalSales >= 2000))
            {
                commissionInfo.CommBonusAmount = 50;
            }
            else if ((_oldTotalSales < 3000 && _newTotalSales >= 3000) ||
                     (_oldTotalSales < 4000 && _newTotalSales >= 4000) ||
                     (_oldTotalSales < 6000 && _newTotalSales >= 6000) ||
                     (_oldTotalSales < 7000 && _newTotalSales >= 7000))
            {
                commissionInfo.CommBonusAmount = 150;
            }
            else if ((_oldTotalSales < 8000 && _newTotalSales >= 8000) ||
                     (_oldTotalSales < 9000 && _newTotalSales >= 9000))
            {
                commissionInfo.CommBonusAmount = 450;
            }
            else if ((_oldTotalSales < 5000 && _newTotalSales >= 5000) ||
                     (_oldTotalSales < 10000 && _newTotalSales >= 10000))
            {
                commissionInfo.CommBonusAmount = 750;
            }
            else
            {
                commissionInfo.CommBonusAmount = 0;
            }

            var totalCommission = commissionInfo.CommBaseAmount + commissionInfo.CommBonusAmount;

            var _oldPromoterPendingBalance = _dbHelper.GroupC_FindPromoterPendingBalance(promoterID);
            _dbHelper.GroupC_UpdatePromoterPendingBalance(promoterID, totalCommission);
            var _newPromoterPendingBalance = _oldPromoterPendingBalance + totalCommission;

            _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
            _logManager.WriteTableChangeLog(_tableLog);

            commissionInfo.CommSettlementDate = DateTime.Now;
            return true;
        }

        public bool ActivatePromoterMoney(string? promoterID, decimal commBaseAmount, decimal commBonusAmount)
        {
            CrmPromoter _promoterInfo = new CrmPromoter();
            if (!FindPromoterInfo(promoterID, ref _promoterInfo))
                return false;

            var totalCommission = commBaseAmount + commBonusAmount;
            var _oldPromoterPendingBalance = _promoterInfo.PendingBalance;
            var _oldPromoterCurrentBalance = _promoterInfo.CurrentBalance;

            _dbHelper.GroupC_UpdatePromoterPendingBalance(promoterID, -totalCommission);
            _dbHelper.GroupC_UpdatePromoterCurrentBalance(promoterID, totalCommission);

            var _newPromoterPendingBalance = _oldPromoterPendingBalance - totalCommission;
            var _newPromoterCurrentBalance = _oldPromoterCurrentBalance + totalCommission;

            var _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { PendingBalance = _oldPromoterPendingBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { PendingBalance = _newPromoterPendingBalance });
            _logManager.WriteTableChangeLog(_tableLog);

            _tableLog = new Log_Auditrails();
            _tableLog.ActionType = "Update";
            _tableLog.TableName = "CRM_PROMOTERS";
            _tableLog.OperatorType = "Platform";
            _tableLog.OperatorId = "\\";
            _tableLog.OldValue = JsonConvert.SerializeObject(new { CurrentBalance = _oldPromoterCurrentBalance });
            _tableLog.NewValue = JsonConvert.SerializeObject(new { CurrentBalance = _newPromoterCurrentBalance });
            _logManager.WriteTableChangeLog(_tableLog);

            return true;
        }

        // ========== 新增功能：团长注册、登录、管理员直接添加 ==========

        /// <summary>
        /// 团长注册（首次注册，待管理员审核激活）
        /// </summary>
        public PromoterRegisterResult RegisterPromoter(PromoterRegisterInfo registerInfo)
        {
            var promoterRegisterResult = new PromoterRegisterResult();
            if (registerInfo == null || string.IsNullOrWhiteSpace(registerInfo.LoginAccount))
            {
                promoterRegisterResult.IsSuccess = false;
                promoterRegisterResult.Message = "输入注册信息不能为空";
                return promoterRegisterResult;
            }

            if (_dbHelper.GroupC_ExistsPromoterByLoginAccount(registerInfo.LoginAccount))
            {
                promoterRegisterResult.IsSuccess = false;
                promoterRegisterResult.Message = "该用户名已存在，请重新输入";
                return promoterRegisterResult;
            }

            string hashedPassword = HashPassword(registerInfo.LoginPassword);

            var promoter = new CrmPromoter
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

            bool result = _dbHelper.GroupC_InsertPromoter(promoter);
            if (!result)
            {
                promoterRegisterResult.IsSuccess = false;
                promoterRegisterResult.Message = "系统异常：添加团长信息失败，请稍后再试";
                return promoterRegisterResult;
            }

            var log = new Log_Auditrails
            {
                TableName = "CRM_PROMOTERS",
                ActionType = "Copy",
                OperatorType = "Platform",
                OperatorId = "\\",
                OldValue = string.Empty,
                NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, promoter.PromoterName, promoter.LoginAccount })
            };
            _logManager.WriteTableChangeLog(log);
            promoterRegisterResult.IsSuccess = true;
            promoterRegisterResult.Message = "";
            return promoterRegisterResult;
        }

        /// <summary>
        /// 团长登录验证
        /// </summary>
        public PromoterLoginResult LoginPromoter(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new PromoterLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            var promoter = _dbHelper.GroupC_FindPromoterByLoginAccount(loginAccount);
            if (promoter == null)
                return new PromoterLoginResult { IsSuccess = false, Message = "账号不存在" };

            string hashedInput = HashPassword(password);
            if (promoter.LoginPassword != hashedInput)
                return new PromoterLoginResult { IsSuccess = false, Message = "密码错误" };

            if (promoter.Status == "Pending")
                return new PromoterLoginResult { IsSuccess = false, Message = "账号尚未审核通过" };
            if (promoter.Status == "Disabled")
                return new PromoterLoginResult { IsSuccess = false, Message = "账号已被禁用" };

            return new PromoterLoginResult
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
         /// <summary>
        /// 管理员直接添加团长（直接生效，无需审核）
        /// </summary>
        public bool AddPromoterByAdmin(PromoterAddInfo addInfo)
        {
            if (addInfo == null || string.IsNullOrWhiteSpace(addInfo.LoginAccount))
                return false;

            if (_dbHelper.GroupC_ExistsPromoterByLoginAccount(addInfo.LoginAccount))
                return false;

            string hashedPassword = HashPassword(addInfo.LoginPassword);

            var promoter = new CrmPromoter
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

            bool result = _dbHelper.GroupC_InsertPromoter(promoter);
            if (!result) return false;

            var log = new Log_Auditrails
            {
                TableName = "CRM_PROMOTERS",
                ActionType = "Copy",
                OperatorType = "Platform",
                OperatorId = "\\",
                OldValue = null,
                NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, promoter.PromoterName, promoter.LoginAccount })
            };
            _logManager.WriteTableChangeLog(log);
            return true;
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