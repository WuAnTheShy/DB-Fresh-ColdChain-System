using FreshColdChain.Interfaces;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;
namespace FreshColdChain.Services
{
    public class SystemAdminService
    {
        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 数据库访问句柄
        private readonly ISysAdminRepository _iSysAdminRepository;
        private readonly IPromoterRepository _ipromoterRepository;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly ITableLogService _logManager;

        // 构造函数
        public SystemAdminService(IUnitOfWork uow, ISysAdminRepository iSysAdminRepository, IPromoterRepository ipromoterRepositor,ITableLogService logManager)
        {
            _uow = uow;
            _iSysAdminRepository = iSysAdminRepository;
            _ipromoterRepository = ipromoterRepositor;
            _logManager = logManager;
        }
        //管理员注册
        public async Task<Result> RegisterAdmin(GroupC_AdminRegisterInfo registerInfo,
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var result = new Result();
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

                bool exists = await _iSysAdminRepository.ExistsUsernameAsync(registerInfo.LoginAccount, transaction);
                if (exists)
                {
                    throw new Exception("该用户名已存在，请重新输入");
                }

                string hashedPassword = HashPassword(registerInfo.LoginPassword);

                var admin = new GroupC_SysUser
                {
                    UserId = "ADM_" + Guid.NewGuid().ToString("N"),
                    RealName = registerInfo.RealName,
                    Username = registerInfo.LoginAccount,
                    PasswordHash = hashedPassword,
                    Phone = registerInfo.Phone,
                    RoleId = "r_admin",
                    Status = "Pending",
                    CreateTime = DateTime.Now
                };

                var saveresult = await _iSysAdminRepository.SaveUserAsync(admin,true,transaction);
                if (!saveresult)
                {
                    throw new Exception("系统异常：添加管理员信息失败，请稍后再试");
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "SYS_USERS",
                    ActionType = "Create",
                    OperatorType = "Platform",
                    OperatorId = "\\",
                    OldValue = string.Empty,
                    NewValue = JsonConvert.SerializeObject(new {
                        UserId = admin.UserId,
                        RealName = admin.RealName,
                        Username = admin.Username,
                        PasswordHash = admin.PasswordHash,
                        Phone = admin.Phone,
                        RoleId = admin.RoleId,
                        Status = admin.Status,
                        CreateTime = admin.CreateTime})
                };
                await _logManager.WriteTableChangeLog(log);
                // 所有业务操作成功，提交事务
                if (ownTransaction)
                    await _uow.CommitAsync();
                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                if (ownTransaction & _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.IsSuccess = false;
                result.ErrorMessage = $"系统错误：{ex.Message}";
                return result;
            }
        }
        //管理员登录
        public GroupC_AdminLoginResult LoginAdmin(string loginAccount, string password)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            // 这里用了同步查询，因为登录不需要事务且快速
            var admin = _iSysAdminRepository.GetUserByName(loginAccount);
            if (admin == null)
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号不存在" };

            string hashedInput = HashPassword(password);
            if (admin.PasswordHash != hashedInput)
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "密码错误" };

            if (admin.Status == "Pending")
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号尚未审核通过" };
            if (admin.Status == "Disable")
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号已被禁用" };

            return new GroupC_AdminLoginResult
            {
                IsSuccess = true,
                UserId = admin.UserId,
                UserName = admin.Username,
            };
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
        //管理员功能：审核团长注册信息
        /// 审核通过团长申请
        public async Task<Result> ApprovePromoterAsync(string promoterId, string adminId)
        {
            var result = new Result();
            await _uow.BeginAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(promoterId))
                    throw new Exception("团长ID不能为空");
                //检查团长是否存在且状态为待审核状态
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);
                if (promoter == null)
                {
                    throw new Exception("该团长不存在");
                }
                if (promoter.Status != "Pending")
                {
                    throw new Exception("该团长账号无需审核或已审核");
                }

                //更新状态为Enable
                bool updated = await _ipromoterRepository.GroupC_UpdatePromoterStatusAsync(promoterId, "Enable", _uow.Transaction);
                if (!updated)
                {
                    throw new Exception("更新状态失败");
                }

                //记录日志
                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = adminId,
                    OldValue = JsonConvert.SerializeObject(new { Status = "Pending" }),
                    NewValue = JsonConvert.SerializeObject(new { Status = "Enable" })
                };
                await _logManager.WriteTableChangeLog(log);

                await _uow.CommitAsync();
                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.IsSuccess = false;
                result.ErrorMessage = $"系统错误：{ex.Message}";
                return result;
            }
        }

        // 审核拒绝团长申请
        public async Task<Result> RejectPromoterAsync(string promoterId, string adminId)
        {
            var result = new Result();
            await _uow.BeginAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(promoterId))
                    throw new Exception("团长ID不能为空");
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);
                if (promoter == null)
                {
                    throw new Exception("该团长不存在");
                }
                if (promoter.Status != "Pending")
                {
                    throw new Exception("该团长账号无需审核或已审核");
                }

                // 拒绝：状态改为 Disable（或你也可选择删除，但保留记录更安全）
                bool updated = await _ipromoterRepository.GroupC_UpdatePromoterStatusAsync(promoterId, "Disable", _uow.Transaction);
                if (!updated)
                {
                    throw new Exception("更新状态失败");
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = adminId,
                    OldValue = JsonConvert.SerializeObject(new { Status = "Pending" }),
                    NewValue = JsonConvert.SerializeObject(new { Status = "Disable" })
                };
                await _logManager.WriteTableChangeLog(log);

                await _uow.CommitAsync();
                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.IsSuccess = false;
                result.ErrorMessage = $"系统错误：{ex.Message}";
                return result;
            }
        }




    }
}