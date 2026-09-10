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
        /// <summary>系统预置头像标识集合（与消费者端 Crm_Customers.Avatar 白名单、前端 ClientApp/src/assets/avatars 一致）。</summary>
        private static readonly HashSet<string> AllowedPromoterAvatars = new(StringComparer.OrdinalIgnoreCase)
        {
            "cat", "rabbit", "panda", "fox",
            "carrot", "broccoli", "tomato", "corn"
        };

        // Repository层句柄
        // 事务核心句柄
        private readonly IUnitOfWork _uow;
        // 数据库访问句柄
        private readonly IPromoterRepository _ipromoterRepository;
        // Interface层句柄（修正字段名，与构造函数一致）
        private readonly ITableLogService _logManager;
        private readonly IPromoterSupplierRepository _ipsRepository;
        // 商品入团表（CRM_PRODUCT_ENTRIES）仓库
        private readonly IPromoterProductRepository _iproductRepository;
		private readonly IPCRRepository _pcrRepository;
		// 商品仓库（用于商品图片）
		private readonly IProductRepository _productRepository;
		// 货物仓库（Inv_Goods，校验供应商上架状态）
		private readonly IGoodsRepository _goodsRepository;
		// 团长图文介绍（文件存储：数据库存相对路径，实际内容为 wwwroot 下 JSON 文件）
		private readonly PromoterIntroStore _introStore;
		// 供应商动态定价引擎：入团时按 商品×供应商×数量×当前时间 计算动态报价（无规则命中=货物售价）
		private readonly IPricingService _pricing;

        // 构造函数
        public PromoterService(IUnitOfWork uow, IPromoterRepository ipromoterRepository, ITableLogService logManager, IPromoterSupplierRepository ipsRepository, IPromoterProductRepository iproductRepository,IPCRRepository pcrRepository, IProductRepository productRepository, IGoodsRepository goodsRepository, PromoterIntroStore introStore, IPricingService pricing)
        {
            _uow = uow;
            _ipromoterRepository = ipromoterRepository;
            _logManager = logManager;
            _ipsRepository = ipsRepository;
            _iproductRepository = iproductRepository;
			_pcrRepository = pcrRepository;
			_productRepository = productRepository;
			_goodsRepository = goodsRepository;
			_introStore = introStore;
			_pricing = pricing;
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
            if (promoterinfo == null)
                return null;

            var avatar = string.IsNullOrWhiteSpace(promoterinfo.Avatar) ? null : promoterinfo.Avatar.Trim();

            return new GroupC_PromoterBasicInfoDto
            {
                PromoterId = promoterinfo.PromoterId,
                PromoterName = promoterinfo.PromoterName,
                Status = promoterinfo.Status,
                Avatar = avatar,
                AvatarUrl = string.IsNullOrWhiteSpace(avatar)
                    ? null
                    : $"/images/avatars/{avatar}.png"
            };
        }

        /// <summary>
        /// 查询团长带货商品（消费者端查看团长带货接口）：
        /// 返回已入团商品的（团长带货介绍存储值、售价、商品图片等）。
        /// 介绍存储值语义：空=无介绍；/uploads/promoter-desc/*.json=图文内容文件相对路径（图文介绍改造后格式）；
        /// 未填写时兜底为供应商商品文字（Description，兼容消费者端纯文本简介）。
        /// </summary>
        public async Task<List<GroupC_FeaturedProductDto>> GetPromoterFeaturedProductsAsync(string promoterId)
        {
            var items = await GetProductEntryDetailsAsync(promoterId);
            // 供应商已下架商品不再对消费者端可见；团长端列表仍展示并标注“已下架”
            return items.Where(p => p.IsProductActive).Select(p => new GroupC_FeaturedProductDto
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                PublishedAt = p.CreateTime,
                Unit = p.Unit,
                SupplierID = p.SupplierID,
                SupplierName = p.SupplierName,
                SupplyPrice = p.SupplyPrice,
                DefaultPrice = p.DefaultPrice,
                Price = p.PromoterPrice ?? p.DefaultPrice,
                PromoterDesc = string.IsNullOrWhiteSpace(p.PromoterDesc) ? (p.Description ?? string.Empty) : p.PromoterDesc,
                Images = p.Images
            }).ToList();
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

            if (GroupC_CrmPromoter.IsPendingStatus(promoter.Status))
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号尚未审核，请耐心等待" };
            if (promoter.Status == "Frozen")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号已被禁用" };
            if (promoter.Status is "Disable" or "Disabled")
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号未审核通过" };
            if (!GroupC_CrmPromoter.IsEnabledStatus(promoter.Status))
                return new GroupC_PromoterLoginResult { IsSuccess = false, Message = "账号未启用" };

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
                    PromoterId = "PRO_" + Guid.NewGuid().ToString("N"),
                    PromoterName = addInfo.PromoterName,
                    LoginAccount = addInfo.LoginAccount,
                    LoginPassword = hashedPassword,
                    Phone = addInfo.Phone,
                    BaseCommissionRate = addInfo.BaseCommissionRate ?? 0.03m,
                    TotalSales = 0,
                    PendingBalance = 0,
                    CurrentBalance = 0,
                    InviteCode = GenerateInviteCode(),
                    // 与自助注册审核通过后的状态一致；"Active" 会被账号管理页当成待审核，且审核列表只查 Pending
                    Status = "Enable",
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



        // 管理员更新团长基础佣金比例（单位与存储一致：小数，如 0.03 表示 3%）
        public async Task<Result> UpdateCommissionRate(GroupC_UpdateCommisionRequest request)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.PromoterId))
                {
                    throw new Exception("更新信息错误：团长编号不能为空");
                }
                if (request.BaseCommissionRate < 0 || request.BaseCommissionRate > 1)
                {
                    throw new Exception("佣金比例必须在 0 ~ 1 之间（如 0.03 表示 3%）");
                }

                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(request.PromoterId, _uow.Transaction);
                if (promoter == null)
                {
                    throw new Exception("该团长不存在");
                }

                var dbResult = await _ipromoterRepository.GroupC_UpdatePromoterCommissionRateAsync(
                    request.PromoterId, request.BaseCommissionRate, _uow.Transaction);
                if (!dbResult)
                {
                    throw new Exception("更新佣金比例失败");
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Platform",
                    OperatorId = "\\",
                    OldValue = JsonConvert.SerializeObject(new { promoter.PromoterId, BaseCommissionRate = promoter.BaseCommissionRate }),
                    NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, BaseCommissionRate = request.BaseCommissionRate })
                };
                await _logManager.WriteTableChangeLog(log);

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


        // 团长自助修改头像（仅限系统预置头像白名单；传 null/空白表示恢复默认文字头像）
        public async Task<Result> UpdatePromoterAvatarAsync(string promoterId, string? avatar)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrWhiteSpace(promoterId))
                {
                    throw new Exception("团长编号不能为空");
                }

                string? normalized = null;
                if (!string.IsNullOrWhiteSpace(avatar))
                {
                    normalized = avatar.Trim();
                    if (!AllowedPromoterAvatars.Contains(normalized))
                        throw new Exception("请选择有效的预置头像");
                }

                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);
                if (promoter == null)
                {
                    throw new Exception("该团长不存在");
                }

                var dbResult = await _ipromoterRepository.GroupC_UpdatePromoterAvatarAsync(promoterId, normalized, _uow.Transaction);
                if (!dbResult)
                {
                    throw new Exception("更新头像失败");
                }

                var log = new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Promoter",
                    OperatorId = promoterId,
                    OldValue = JsonConvert.SerializeObject(new { promoter.PromoterId, Avatar = promoter.Avatar }),
                    NewValue = JsonConvert.SerializeObject(new { promoter.PromoterId, Avatar = normalized })
                };
                await _logManager.WriteTableChangeLog(log);

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

        public async Task<Result> BindPayAccountAsync(string promoterId, string platform, string accountNo)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrWhiteSpace(promoterId))
                    throw new Exception("团长编号不能为空");
                if (!PromoterPayAccounts.IsValidPlatform(platform))
                    throw new Exception("请选择微信、支付宝或银行卡");

                var (ok, error) = PromoterPayAccounts.ValidateAccount(platform, accountNo);
                if (!ok)
                    throw new Exception(error);

                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);
                if (promoter == null)
                    throw new Exception("该团长不存在");

                var normalizedPlatform = PromoterPayAccounts.Normalize(platform);
                var normalizedAccount = accountNo.Trim();
                if (normalizedPlatform == PromoterPayAccounts.BankCard)
                    normalizedAccount = System.Text.RegularExpressions.Regex.Replace(normalizedAccount, @"[\s-]", string.Empty);

                var dbResult = await _ipromoterRepository.GroupC_UpdatePromoterPayAccountAsync(
                    promoterId, normalizedPlatform, normalizedAccount, _uow.Transaction);
                if (!dbResult)
                    throw new Exception("绑定收款账户失败");

                await _logManager.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Promoter",
                    OperatorId = promoterId,
                    OldValue = JsonConvert.SerializeObject(new { Platform = normalizedPlatform, Account = promoter.GetBoundPayAccount(normalizedPlatform) }),
                    NewValue = JsonConvert.SerializeObject(new { Platform = normalizedPlatform, Account = normalizedAccount }),
                    RecordId = promoterId
                });

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

        public async Task<Result> UnbindPayAccountAsync(string promoterId, string platform)
        {
            await _uow.BeginAsync();
            var _result = new Result();
            try
            {
                if (string.IsNullOrWhiteSpace(promoterId))
                    throw new Exception("团长编号不能为空");
                if (!PromoterPayAccounts.IsValidPlatform(platform))
                    throw new Exception("请选择微信、支付宝或银行卡");

                var promoter = await _ipromoterRepository.GroupC_FindPromoterRecordAsync(promoterId, _uow.Transaction);
                if (promoter == null)
                    throw new Exception("该团长不存在");

                var normalizedPlatform = PromoterPayAccounts.Normalize(platform);
                var dbResult = await _ipromoterRepository.GroupC_UpdatePromoterPayAccountAsync(
                    promoterId, normalizedPlatform, string.Empty, _uow.Transaction);
                if (!dbResult)
                    throw new Exception("解绑收款账户失败");

                await _logManager.WriteTableChangeLog(new GroupC_LogAuditrails
                {
                    TableName = "CRM_PROMOTERS",
                    ActionType = "Update",
                    OperatorType = "Promoter",
                    OperatorId = promoterId,
                    OldValue = JsonConvert.SerializeObject(new { Platform = normalizedPlatform, Account = promoter.GetBoundPayAccount(normalizedPlatform) }),
                    NewValue = JsonConvert.SerializeObject(new { Platform = normalizedPlatform, Account = (string?)null }),
                    RecordId = promoterId
                });

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

        //============================团长-供应商合作服务===================================
        public async Task<List<string>> GetActiveSupplierIdsAsync(string promoterId)
        {
            var fromRelation = await _ipsRepository.GetActiveSupplierIdsByPromoterAsync(promoterId, _uow.Transaction);
            var fromEntries = await _iproductRepository.GetActiveEntriesByPromoterAsync(promoterId, _uow.Transaction);
            // 入团货盘才是消费者可见的供货关系；CRM_PSRELATION 可能漏写，不能单独作为授权依据
            return fromRelation
                .Concat(fromEntries.Select(entry => entry.SupplierId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        //============================团长-商品入团服务（商品入团表 CRM_PRODUCT_ENTRIES）===================================
        // 说明：入团商品 =（团长，商品，供应商）三元组。团长与商品为多对多，
        //       同一商品可由不同供应商供货，故以“商品+供应商”组合为绑定单位。

        /// <summary>
        /// 更新已入团（商品，供应商）组合的团长定价。
        /// 校验规则同入团：|团长价 - 推荐价| &lt; |推荐价 - 报价| / 2；未填写则默认取推荐价。
        /// </summary>
        public async Task<bool> UpdateEntryPriceAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice, decimal supplyPrice, decimal defaultPrice)
        {
            var price = promoterPrice ?? defaultPrice;
            var allowedDiff = Math.Abs(defaultPrice - supplyPrice) / 2m;
            var actualDiff = Math.Abs(price - defaultPrice);
            if (allowedDiff == 0 ? actualDiff != 0 : actualDiff >= allowedDiff)
            {
                throw new InvalidOperationException(
                    $"定价超出允许范围：|团长价({price:F2}) - 推荐价({defaultPrice:F2})| 必须小于 |推荐价 - 供应商动态定价| / 2 = {allowedDiff:F2}");
            }

            await _uow.BeginAsync();
            try
            {
                var result = await _iproductRepository.UpdateEntryPriceAsync(promoterId, productId, supplierId, price, _uow.Transaction);
                await _uow.CommitAsync();
                return result;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        /// <summary>查询团长已入团商品详情列表（含商品名、供应商名、报价、推荐价、团长定价、文字介绍、图片）</summary>
        public async Task<List<PromoterProductEntryDetailDto>> GetProductEntryDetailsAsync(string promoterId)
        {
            var items = await _iproductRepository.GetActiveEntriesDetailAsync(promoterId, _uow.Transaction);
            await AttachProductImagesAsync(items);
            return items;
        }

        /// <summary>
        /// 为已入团商品详情批量附加商品图片：按（供应商×商品）过滤——
        /// 该供应商自己上传的图在前，平台通用图在后，取前 3 张。
        /// </summary>
        private async Task AttachProductImagesAsync(IEnumerable<PromoterProductEntryDetailDto> items)
        {
            var allImages = (await _productRepository.GetAllProductImagesAsync())
                .Where(img => img.HasData) // 仅附真正有二进制数据的图片
                .ToList();

            foreach (var item in items)
            {
                item.Images = allImages
                    .Where(img => img.ProductID == item.ProductID
                                  && (img.SupplierID == item.SupplierID || img.SupplierID == null))
                    .OrderBy(img => img.SupplierID != item.SupplierID)
                    .ThenBy(img => img.SortOrder)
                    .ThenBy(img => img.CreateTime)
                    .Take(3)
                    .Select(img => img.ImageUrl)
                    .ToList();
            }
        }

        /// <summary>查询团长当前所有已入团的（商品，供应商，团长定价）组合</summary>
        public async Task<List<(string ProductId, string SupplierId, decimal? PromoterPrice)>> GetActiveProductEntriesAsync(string promoterId)
        {
            return await _iproductRepository.GetActiveEntriesByPromoterAsync(promoterId, _uow.Transaction);
        }

        /// <summary>查询指定（商品，供应商）入团组合的详情（含商品图）；不存在或非上架状态返回 null</summary>
        public async Task<PromoterProductEntryDetailDto?> GetProductEntryDetailAsync(string promoterId, string productId, string supplierId)
        {
            var item = await _iproductRepository.GetActiveEntryDetailAsync(promoterId, productId, supplierId, _uow.Transaction);
            if (item != null)
                await AttachProductImagesAsync(new[] { item });
            return item;
        }

        /// <summary>
        /// 将（商品，供应商）加入团长入团商品（重复加入则自动恢复 Active）。
        /// promoterPrice 为团长定价：未填写（null）时默认取推荐价；
        /// 填写时须满足定价规则 |团长价 - 推荐价| &lt; |推荐价 - 报价| / 2，否则抛异常。
        /// 供应商报价 = 供应商动态定价：入团时刻调用 A 组规则引擎
        /// （商品×供应商×数量1×当前时间）计算最终报价，无规则命中即货物售价；
        /// 推荐价 = 报价 × 1.2（倍率不变）。两者随入团快照落库到
        /// CRM_PRODUCT_ENTRIES.SUPPLYPRICE / DEFAULTPRICE，团长端/消费者端此后读取快照。
        /// description 为供应商商品文字：作为团长带货介绍默认值（默认复制供应商文字，团长可自行修改）。
        /// </summary>
        public async Task<bool> AddProductEntryAsync(string promoterId, string productId, string supplierId, decimal? promoterPrice, string? description = null)
        {
            // 该供应商已下架该货物（Inv_Goods.Status != 'ACTIVE'）或未建立货物时不允许入团
            var goods = await _goodsRepository.GetAsync(productId, supplierId);
            if (goods == null || !string.Equals(goods.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该供应商已下架该商品，暂不可入团，请等待供应商恢复上架后再操作。");
            }

            // 报价 = 供应商动态定价：按当前时间/数量1计算规则命中后的最终供货价；引擎异常时回退货物售价
            var supplyPrice = goods.SalePrice;
            try
            {
                var calc = await _pricing.CalculatePriceAsync(new PriceCalculationRequest
                {
                    ProductID = productId,
                    SupplierID = supplierId,
                    Quantity = 1m // 团长进价按单件询价；批量优惠在最终零售/下单场景体现
                });
                if (calc.IsSuccess && calc.Data != null)
                {
                    supplyPrice = calc.Data.FinalPrice;
                }
            }
            catch
            {
                // 忽略规则引擎异常，回退静态售价，保证入团流程可用
            }

            // 推荐价(建议零售) = 动态报价 × 1.2（倍率与历史一致）
            var defaultPrice = Math.Round(supplyPrice * 1.2m, 2);
            var price = promoterPrice ?? defaultPrice;

            // 定价规则：|团长价 - 推荐价| < |推荐价 - 报价| / 2
            var allowedDiff = Math.Abs(defaultPrice - supplyPrice) / 2m;
            var actualDiff = Math.Abs(price - defaultPrice);
            if (allowedDiff == 0 ? actualDiff != 0 : actualDiff >= allowedDiff)
            {
                throw new InvalidOperationException(
                    $"定价超出允许范围：|团长价({price:F2}) - 推荐价({defaultPrice:F2})| 必须小于 |推荐价 - 供应商动态定价| / 2 = {allowedDiff:F2}");
            }

            await _uow.BeginAsync();
            try
            {
                // supplyPrice/defaultPrice 作为动态报价快照随入团写入 CRM_PRODUCT_ENTRIES
                var result = await _iproductRepository.AddOrUpdateEntryAsync(
                    promoterId, productId, supplierId, price, supplyPrice, defaultPrice, description, "Active", _uow.Transaction);
                // 入团即表示与该供应商合作；下单校验会读 CRM_PSRELATION，必须同步写入
                await _ipsRepository.AddOrUpdateRelationAsync(promoterId, supplierId, "Active", _uow.Transaction);
                await _uow.CommitAsync();
                return result;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 更新已入团（商品，供应商）组合的团长带货介绍。
        /// 团长端提交的是富文本 JSON（title + 多段 text/images）：
        /// 保存时把图文内容写入 wwwroot/uploads/promoter-desc/ 下 JSON 文件，
        /// 数据库 PROMOTERDESC 仅存该文件的相对路径；内容为空时删除文件并置空。
        /// </summary>
        public async Task<bool> UpdateEntryDescriptionAsync(string promoterId, string productId, string supplierId, string? contentJson)
        {
            // 解析（非法 JSON 会返回错误信息并抛异常），空内容代表“清除介绍”
            var (content, error) = PromoterIntroStore.TryParse(contentJson);
            if (error != null) throw new InvalidOperationException(error);

            var stored = await _introStore.SaveAsync(promoterId, productId, supplierId, content);

            await _uow.BeginAsync();
            try
            {
                var result = await _iproductRepository.UpdateEntryDescriptionAsync(promoterId, productId, supplierId, stored, _uow.Transaction);
                await _uow.CommitAsync();
                return result;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 查询“该供应商上传的该商品”的全部图片（一次全量返回，供团长编辑图文介绍时选择插入）。
        /// 商品图片按商品维度存放于 Inv_ProductImages，不截取数量限制。
        /// </summary>
        public async Task<List<string>> GetSupplierProductImagesAsync(string productId)
        {
            var images = await _productRepository.GetAllProductImagesAsync();
            return images
                .Where(img => string.Equals(img.ProductID, productId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(img => img.SortOrder)
                .ThenBy(img => img.CreateTime)
                .Select(img => img.ImageUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 消费者端读取「团长推文」：团长对某商品的图文介绍（标题 + 段落）。
        /// 仅返回该团长有效入团且平台在售（IsProductActive）的商品；
        /// 商品不在该团长在售列表返回 null；无介绍内容返回 HasIntro=false。
        /// </summary>
        public async Task<GroupC_PromoterIntroResult?> GetProductIntroAsync(string promoterId, string productId)
        {
            if (string.IsNullOrWhiteSpace(promoterId) || string.IsNullOrWhiteSpace(productId)) return null;

            var items = await GetProductEntryDetailsAsync(promoterId);
            var entry = items.FirstOrDefault(x =>
                string.Equals(x.ProductID, productId, StringComparison.OrdinalIgnoreCase) && x.IsProductActive);
            if (entry == null) return null;

            var rich = await _introStore.LoadAsync(entry.PromoterDesc);
            if (rich == null || rich.IsEmpty())
                return new GroupC_PromoterIntroResult();

            return new GroupC_PromoterIntroResult
            {
                HasIntro = true,
                Title = rich.Title,
                Sections = rich.Sections
            };
        }

        /// <summary>将（商品，供应商）从团长入团商品中移除（软删除）</summary>
        public async Task<bool> RemoveProductEntryAsync(string promoterId, string productId, string supplierId)
        {
            await _uow.BeginAsync();
            try
            {
                var result = await _iproductRepository.SoftDeleteEntryAsync(promoterId, productId, supplierId, _uow.Transaction);
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

        public async Task<Result> UnbindCustomerFromPromoterAsync(
            string customerId,
            string promoterId,
            CancellationToken cancellationToken = default)
        {
            var result = new Result();
            if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(promoterId))
            {
                result.ErrorMessage = "消费者ID和团长ID不能为空";
                return result;
            }

            await _uow.BeginAsync();
            try
            {
                await _pcrRepository.DeleteRelationAsync(
                    customerId.Trim(),
                    promoterId.Trim(),
                    _uow.Transaction);
                await _uow.CommitAsync();
                result.IsSuccess = true;
                return result;
            }
            catch (Exception exception)
            {
                if (_uow.Connection.State == ConnectionState.Open)
                    await _uow.RollbackAsync();
                result.ErrorMessage = $"系统错误：{exception.Message}";
                return result;
            }
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
