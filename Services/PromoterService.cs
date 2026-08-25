using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChainSystem.Repositories;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;
namespace FreshColdChain.Services
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
        private readonly IPromoterSupplierRepository _ipsRepository;
        private readonly IPCRRepository _pcrRepository;
        // 构造函数
        public PromoterService(IUnitOfWork uow, IPromoterRepository ipromoterRepository, ITableLogService logManager, IPromoterSupplierRepository ipsRepository, IPCRRepository pcrRepository)
        {
            _uow = uow;
            _ipromoterRepository = ipromoterRepository;
            _logManager = logManager;
            _ipsRepository = ipsRepository;
            _pcrRepository = pcrRepository;
        }


        public async Task<GroupC_PagedResult<GroupC_AvailablePromoterDto>> GetAvailablePromotersAsync(
        GroupC_AvailablePromoterQuery query,
        CancellationToken cancellationToken = default)
        {
            var pageIndex = Math.Max(1, query.PageIndex);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var skip = (pageIndex - 1) * pageSize;

            var result = await _ipromoterRepository.GetAvailablePromotersAsync(
                query.Keyword, skip, pageSize, _uow.Transaction);

            return new GroupC_PagedResult<GroupC_AvailablePromoterDto>
            {
                Pageindex = pageIndex,              
                PageSize = pageSize,
                TotalCount = result.TotalCount,   
                Items = result.Items.ToList()
            };
        }


        public async Task<GroupC_PromoterBasicInfoDto?> GetPromoterBasicInfoAsync(
        string promoterId,
        CancellationToken cancellationToken = default)
        {
            var promoterinfo = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);


            return new GroupC_PromoterBasicInfoDto
            {
                PromoterId = promoterinfo.PromoterId,
                PromoterName = promoterinfo.PromoterName,
                Status = promoterinfo.Status,
            };
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

        // 查询全部团长（管理端启禁用列表用）
        public async Task<IEnumerable<GroupC_CrmPromoter>> GetAllPromotersAsync()
        {
            return await _ipromoterRepository.GroupC_GetAllPromotersAsync();
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



        //============================团长-供应商合作服务===================================
        public async Task<List<string>> GetActiveSupplierIdsAsync(string promoterId)
        {
            return await _ipsRepository.GetActiveSupplierIdsByPromoterAsync(promoterId, _uow.Transaction);
        }

        public async Task<Dictionary<string, bool>> ValidateSuppliersAsync(string promoterId, List<string> supplierIds)
        {
            if (supplierIds == null || !supplierIds.Any())
                return new Dictionary<string, bool>();
            return await _ipsRepository.ValidateRelationsAsync(promoterId, supplierIds, _uow.Transaction);
        }

        public async Task<bool> AddRelationAsync(string promoterId, string supplierId)
        {
            await _uow.BeginAsync();
            try
            {
                var result = await _ipsRepository.AddOrUpdateRelationAsync(promoterId, supplierId, "Active", _uow.Transaction);
                await _uow.CommitAsync();
                return result;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> RemoveRelationAsync(string promoterId, string supplierId)
        {
            await _uow.BeginAsync();
            try
            {
                var result = await _ipsRepository.SoftDeleteRelationAsync(promoterId, supplierId, _uow.Transaction);
                await _uow.CommitAsync();
                return result;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        //============================团长-消费者绑定服务===================================
        public async Task<Result> BindCustomerToPromoterAsync(
            string customerId,
            string promoterId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var bindResult = new Result();
            bool ownTransaction = false;
            try
            {
                if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(promoterId))
                {
                    bindResult.ErrorMessage = "消费者ID和团长ID不能为空";
                    return bindResult;
                }

                customerId = customerId.Trim();
                promoterId = promoterId.Trim();

                if (transaction == null)
                {
                    await _uow.BeginAsync();
                    ownTransaction = true;
                    transaction = _uow.Transaction;
                }
                else if (_uow.Transaction == null)
                {
                    _uow.AttachExternalTransaction(transaction);
                }

                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, transaction);
                if (promoter == null)
                {
                    throw new Exception("团长不存在");
                }

                var exists = await _pcrRepository.ExistsRelationAsync(customerId, promoterId, transaction);
                if (exists)
                {
                    if (ownTransaction)
                        await _uow.CommitAsync();
                    bindResult.IsSuccess = true;
                    return bindResult;
                }

                var inserted = await _pcrRepository.InsertRelationAsync(customerId, promoterId, transaction);
                if (!inserted)
                {
                    throw new Exception("插入绑定记录失败");
                }

                if (ownTransaction)
                    await _uow.CommitAsync();
                bindResult.IsSuccess = true;
                return bindResult;
            }
            catch (Exception ex)
            {
                if (ownTransaction && _uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                bindResult.IsSuccess = false;
                bindResult.ErrorMessage = $"系统错误：{ex.Message}";
                return bindResult;
            }
        }

        public async Task<List<string>> GetBoundPromoterIdsAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
                return new List<string>();
            return await _pcrRepository.GetPromoterIdsByCustomerAsync(customerId.Trim(), _uow.Transaction);
        }

        public async Task<List<GroupC_CrmPCRelation>> GetBoundCustomersByPromoterAsync(string promoterId)
        {
            if (string.IsNullOrWhiteSpace(promoterId))
                return new List<GroupC_CrmPCRelation>();
            return await _pcrRepository.GetRelationsByPromoterAsync(promoterId.Trim(), _uow.Transaction);
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
