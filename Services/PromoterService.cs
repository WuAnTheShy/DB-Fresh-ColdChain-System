using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Repositories;
using FreshColdChain.Repositories;
using DBFreshColdChain.Models;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;
namespace DBFreshColdChain.Services
{
    public class PromoterService: IPromoterService
    {
        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 数据库访问句柄
        private readonly IPromoterRepository _ipromoterRepository;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly ITableLogService _logManager;
              
        // 构造函数
        public PromoterService(IUnitOfWork uow, IPromoterRepository ipromoterRepository, ITableLogService logManager)
        {
            _uow = uow;
            _ipromoterRepository = ipromoterRepository;
            _logManager = logManager;
        }

        

        
        // ========== 新增功能：团长注册、登录、管理员直接添加 ==========

        // 团长注册（首次注册，待管理员审核激活）
        //团长注册
        public async Task<GroupC_PromoterRegisterResult> RegisterPromoter(GroupC_PromoterRegisterInfo registerInfo,
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var promoterRegisterResult = new GroupC_PromoterRegisterResult();
            bool ownTransaction = false;
            try
            {
                if (transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;
                }

                if (registerInfo == null || string.IsNullOrWhiteSpace(registerInfo.LoginAccount))
                {
                    throw new Exception("输入注册信息不能为空");
                }

                bool exists = await _ipromoterRepository.GroupC_ExistsPromoterByLoginAccountAsync(registerInfo.LoginAccount, transaction);
                if (exists)
                {
                    throw new Exception("该用户名已存在，请重新输入");
                }

                string hashedPassword = HashPassword(registerInfo.LoginPassword);

                var promoter = new GroupC_CrmPromoter
                {
                    PromoterId = "PRO_"+Guid.NewGuid().ToString("N"),
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

                var result = await _ipromoterRepository.GroupC_InsertPromoterAsync(promoter, transaction);
                if (!result)
                {
                    throw new Exception("系统异常：添加团长信息失败，请稍后再试");
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Create",
                    OperatorType = "Platform",
                    OperatorId = "\\",
                    OldValue = string.Empty,
                    NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, promoter.PromoterName, promoter.LoginAccount })
                };
                await _logManager.WriteTableChangeLog(log);
                // 所有业务操作成功，提交事务
                if(ownTransaction)
                    await _uow.CommitAsync();
                promoterRegisterResult.IsSuccess = true;
                return promoterRegisterResult;
            }
            catch (Exception ex)
            {
                if(ownTransaction & _uow.Connection.State == ConnectionState.Open)
                   await _uow.RollbackAsync();
                promoterRegisterResult.IsSuccess = false;
                promoterRegisterResult.Message = $"系统错误：{ex.Message}";
                return promoterRegisterResult;
            }
        }

        // 团长登录验证
        public GroupC_PromoterLoginResult LoginPromoter(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            // 这里用了同步查询，因为登录不需要事务且快速
            var promoter = _ipromoterRepository.GroupC_FindPromoterByLoginAccount(loginAccount);
            if (promoter == null)
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号不存在" };

            string hashedInput = HashPassword(password);
            if (promoter.LoginPassword != hashedInput)
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "密码错误" };

            if (promoter.Status == "Pending")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号尚未审核，请耐心等待" };
            if (promoter.Status == "Frozen")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号已被禁用" };
            if (promoter.Status == "Disable")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号未审核通过" };

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
        // 管理员直接添加团长（直接生效，无需审核）
        public async Task<Result>AddPromoterByAdmin(GroupC_PromoterAddInfo addInfo)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (addInfo == null || string.IsNullOrWhiteSpace(addInfo.LoginAccount))
                {
                    throw new Exception("添加信息错误");
                }

                bool exists = await _ipromoterRepository.GroupC_ExistsPromoterByLoginAccountAsync(addInfo.LoginAccount, _uow.Transaction);
                if (exists)
                {
                    throw new Exception("已存在该团长信息，无需添加");
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

                var dbResult = await _ipromoterRepository.GroupC_InsertPromoterAsync(promoter, _uow.Transaction);
                if (!dbResult)
                {
                    throw new Exception("插入该记录失败");
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
                await _logManager.WriteTableChangeLog(log);
                // 所有业务操作成功，提交事务
                await _uow.CommitAsync();
                _result.IsSuccess = true;
                return _result;
            }
            catch (Exception ex)
            {
                // 任何一步报错，回滚所有操作
                if(_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                _result.IsSuccess = false;
                _result.ErrorMessage = $"系统错误：{ex.Message}";
                return _result;
            }
           
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

        public async Task<List<GroupC_CrmPromoter>> GetPendingPromotersAsync()
        {
            // 直接使用Repository查询所有状态为Pending的团长
            var Pendinglist = await _ipromoterRepository.GroupC_GetPromotersByStatusAsync("Pending");
            return Pendinglist.ToList();
        }

    }
}
