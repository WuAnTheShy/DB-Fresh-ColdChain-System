using FreshColdChain.Interfaces;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Filters;
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
        //管理员注册（自助注册默认 Pending，须审核；管理员代建可直接 Enabled）
        public Task<Result> RegisterAdmin(GroupC_AdminRegisterInfo registerInfo,
            IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => CreateAdminAsync(registerInfo, requireReview: true, transaction, cancellationToken);

        public Task<Result> AddAdminByAdmin(GroupC_AdminRegisterInfo registerInfo)
            => CreateAdminAsync(registerInfo, requireReview: false);

        private async Task<Result> CreateAdminAsync(GroupC_AdminRegisterInfo registerInfo, bool requireReview,
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

                string adminKind = NormalizeAdminKind(registerInfo.AdminKind);

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
                    AdminKind = adminKind,
                    Status = requireReview ? "Pending" : "Enabled",
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
                    OperatorType = requireReview ? "Platform" : "Admin",
                    OperatorId = "\\",
                    OldValue = string.Empty,
                    NewValue = JsonConvert.SerializeObject(new {
                        UserId = admin.UserId,
                        RealName = admin.RealName,
                        Username = admin.Username,
                        PasswordHash = admin.PasswordHash,
                        Phone = admin.Phone,
                        RoleId = admin.RoleId,
                        AdminKind = admin.AdminKind,
                        Status = admin.Status,
                        CreateTime = admin.CreateTime})
                };
                await _logManager.WriteTableChangeLog(log);
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
        //管理员启用/禁用团长账号（仅 Enable <-> Disable 互转；Pending 需走注册审核流程）
        public async Task<Result> SetPromoterStatus(string adminId, string promoterId, string targetStatus,
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
                if (targetStatus is not ("Enable" or "Disable"))
                {
                    throw new Exception("非法的目标状态");
                }
                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, transaction);
                if (promoter == null)
                {
                    throw new Exception("团长不存在");
                }
                if (GroupC_CrmPromoter.IsPendingStatus(promoter.Status))
                {
                    throw new Exception("待审核的团长请先到注册审核中处理");
                }
                if (promoter.Status == targetStatus)
                {
                    throw new Exception("该团长已处于目标状态，无需变更");
                }

                var oldStatus = promoter.Status;
                await _ipromoterRepository.GroupC_UpdatePromoterStatusAsync(promoterId, targetStatus, transaction);

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = adminId,
                    OldValue = JsonConvert.SerializeObject(new { Status = oldStatus }),
                    NewValue = JsonConvert.SerializeObject(new { Status = targetStatus })
                };
                await _logManager.WriteTableChangeLog(log);

                if (ownTransaction)
                    await _uow.CommitAsync();
                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.IsSuccess = false;
                result.ErrorMessage = $"系统错误：{ex.Message}";
                return result;
            }
        }

        //管理员登录（登录时须指明管理员种类，与账号注册时选择的种类一致才放行）
        public GroupC_AdminLoginResult LoginAdmin(string loginAccount, string password, string? adminKind = null)
        {
            if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号或密码不能为空" };

            if (!AdminSession.IsValidKind(adminKind))
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "请选择正确的管理员类型" };

            // 这里用了同步查询，因为登录不需要事务且快速
            var admin = _iSysAdminRepository.GetUserByName(loginAccount);
            if (admin == null)
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号不存在" };

            string hashedInput = HashPassword(password);
            if (admin.PasswordHash != hashedInput)
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "密码错误" };

            if (admin.Status == "Pending")
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号尚未审核通过，请联系账号管理员审核" };
            if (!string.Equals(admin.Status, "Enable", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(admin.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
                return new GroupC_AdminLoginResult { IsSuccess = false, Message = "账号未启用或已被锁定" };

            // 种类校验：存量账号缺省按账号管理员处理
            string actualKind = string.IsNullOrEmpty(admin.AdminKind) ? AdminSession.AccountKind : admin.AdminKind;
            if (!string.Equals(actualKind, adminKind, StringComparison.OrdinalIgnoreCase))
                return new GroupC_AdminLoginResult
                {
                    IsSuccess = false,
                    Message = $"该账号是「{AdminSession.KindName(actualKind)}」，与您选择的类型不符，请重新选择"
                };

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
        // 审核通过团长申请
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

        // 管理员种类与管理员账号审核

        // 把未填/填错的类型规范化为账号管理员。
        private static string NormalizeAdminKind(string? kind)
            => AdminSession.IsValidKind(kind) ? kind! : AdminSession.AccountKind;

        // 待审核管理员列表（注册后状态 Pending，需账号管理员审核）。
        public async Task<List<GroupC_SysUser>> GetPendingAdminsAsync()
        {
            return await _iSysAdminRepository.GetUsersByStatusAsync("Pending");
        }

        // 全部管理员（用于账号管理）。
        public async Task<List<GroupC_SysUser>> GetAllAdminsAsync()
        {
            return await _iSysAdminRepository.GetAllUsersAsync();
        }

        // 管理员注册审核通过：Pending -&gt; Enabled。
        public async Task<Result> ApproveAdminAsync(string userId, string operatorId)
        {
            var result = new Result();
            await _uow.BeginAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    throw new Exception("管理员ID不能为空");

                var user = await _iSysAdminRepository.GetUserByIdAsync(userId, _uow.Transaction);
                if (user == null)
                    throw new Exception("该管理员账号不存在");
                if (user.Status != "Pending")
                    throw new Exception("该账号无需审核或已审核");

                bool updated = await _iSysAdminRepository.UpdateUserStatusAsync(userId, "Enabled", _uow.Transaction);
                if (!updated)
                    throw new Exception("更新状态失败");

                await _logManager.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "SYS_USERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = operatorId,
                    OldValue = JsonConvert.SerializeObject(new { UserId = userId, Status = "Pending" }),
                    NewValue = JsonConvert.SerializeObject(new { UserId = userId, Status = "Enabled" })
                });

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

        // 管理员注册审核拒绝：Pending -&gt; Disabled（保留记录可追溯）。
        public async Task<Result> RejectAdminAsync(string userId, string operatorId, string? reason = null)
        {
            var result = new Result();
            await _uow.BeginAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    throw new Exception("管理员ID不能为空");

                var user = await _iSysAdminRepository.GetUserByIdAsync(userId, _uow.Transaction);
                if (user == null)
                    throw new Exception("该管理员账号不存在");
                if (user.Status != "Pending")
                    throw new Exception("该账号无需审核或已审核");

                bool updated = await _iSysAdminRepository.UpdateUserStatusAsync(userId, "Disabled", _uow.Transaction);
                if (!updated)
                    throw new Exception("更新状态失败");

                await _logManager.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "SYS_USERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = operatorId,
                    OldValue = JsonConvert.SerializeObject(new { UserId = userId, Status = "Pending" }),
                    NewValue = JsonConvert.SerializeObject(new { UserId = userId, Status = "Disabled", Reason = reason })
                });

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

        // 管理员启用/禁用（仅 Enabled &lt;-&gt; Disabled 互转；Pending 需走注册审核）。
        public async Task<Result> SetAdminStatus(string operatorId, string userId, string targetStatus)
        {
            var result = new Result();
            if (targetStatus is not ("Enabled" or "Disabled"))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "非法的目标状态";
                return result;
            }
            await _uow.BeginAsync();
            try
            {
                var user = await _iSysAdminRepository.GetUserByIdAsync(userId, _uow.Transaction);
                if (user == null)
                    throw new Exception("该管理员账号不存在");
                if (user.IsPendingAccount)
                    throw new Exception("待审核的管理员请先到注册审核中处理");
                if (user.Status == targetStatus)
                    throw new Exception("该账号已处于目标状态，无需变更");
                if (operatorId == userId)
                    throw new Exception("不能操作自己的账号状态");

                bool updated = await _iSysAdminRepository.UpdateUserStatusAsync(userId, targetStatus, _uow.Transaction);
                if (!updated)
                    throw new Exception("更新状态失败");

                await _logManager.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "SYS_USERS",
                    ActionType = "Update",
                    OperatorType = "Admin",
                    OperatorId = operatorId,
                    OldValue = JsonConvert.SerializeObject(new { UserId = userId, Status = user.Status }),
                    NewValue = JsonConvert.SerializeObject(new { UserId = userId, Status = targetStatus })
                });

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
