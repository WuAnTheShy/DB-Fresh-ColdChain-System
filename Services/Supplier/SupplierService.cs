using System.Security.Cryptography;
using System.Text;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services.Supplier;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repo;
    private readonly ISupplierPriceRepository _priceRepo;
    private readonly IProductRepository _productRepo;

    public SupplierService(ISupplierRepository repo, ISupplierPriceRepository priceRepo, IProductRepository productRepo)
    {
        _repo = repo;
        _priceRepo = priceRepo;
        _productRepo = productRepo;
    }

    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize)
    {
        var (items, total) = await _repo.GetPagedWithProductCountAsync(pageIndex, pageSize);
        return ApiResponse<PagedResult<SupplierDto>>.Success(new PagedResult<SupplierDto>
        {
            PageIndex = pageIndex, PageSize = pageSize, TotalCount = total,
            Items = items.Select(MapToDto).ToList()
        });
    }

    public async Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(string id)
    {
        var s = await _repo.GetByIdWithProductsAsync(id);
        if (s == null) return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);
        return ApiResponse<SupplierDto>.Success(MapToDto(s));
    }

    public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto)
    {
        try
        {
            var s = new InvSupplier
            {
                SupplierName = dto.SupplierName, LicenseNo = dto.LicenseNo,
                ExpiryDate = dto.ExpiryDate, CreditLevel = dto.CreditLevel,
                ContactPhone = dto.ContactPhone, LoginAccount = dto.LoginAccount,
                LoginPassword = string.IsNullOrEmpty(dto.LoginPassword) ? null : HashPassword(dto.LoginPassword)
            };
            await _repo.AddAsync(s);
            return ApiResponse<SupplierDto>.Success(MapToDto(s), "供应商创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<SupplierDto>.Fail($"供应商创建失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(string id, CreateSupplierDto dto)
    {
        try
        {
            var s = await _repo.GetByIdAsync(id);
            if (s == null) return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);
            s.SupplierName = dto.SupplierName; s.LicenseNo = dto.LicenseNo;
            s.ExpiryDate = dto.ExpiryDate; s.CreditLevel = dto.CreditLevel;
            s.ContactPhone = dto.ContactPhone; s.LoginAccount = dto.LoginAccount;
            if (!string.IsNullOrEmpty(dto.LoginPassword)) s.LoginPassword = HashPassword(dto.LoginPassword);
            _repo.Update(s);
            return ApiResponse<SupplierDto>.Success(MapToDto(s), "供应商更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<SupplierDto>.Fail($"供应商更新失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteSupplierAsync(string id)
    {
        try
        {
            var s = await _repo.GetByIdWithProductsAsync(id);
            if (s == null) return ApiResponse.Fail("供应商不存在", 404);

            // 外键保护：有关联产品时拒绝删除
            if (s.Products.Any())
                return ApiResponse.Fail($"无法删除：该供应商下有 {s.Products.Count} 个产品，请先处理");

            _repo.Delete(s);
            return ApiResponse.Success("供应商已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"供应商删除失败：{ex.Message}");
        }
    }

    // ========== 供货价（进价由供应商决定）==========

    /// <summary>供应商详情页用：该供应商已报价的所有产品（多供应商模式下，报价即供货关系）</summary>
    public async Task<ApiResponse<List<SupplierProductQuoteDto>>> GetSupplierProductQuotesAsync(string supplierId)
    {
        try
        {
            var quotes = await _priceRepo.GetQuotesBySupplierAsync(supplierId);
            var products = await _productRepo.GetAllAsync();
            var productMap = products.ToDictionary(p => p.ProductID);

            var list = quotes.Select(q =>
            {
                productMap.TryGetValue(q.ProductID, out var p);
                return new SupplierProductQuoteDto
                {
                    ProductID = q.ProductID,
                    ProductName = p?.ProductName ?? q.ProductID,
                    SupplyPrice = q.SupplyPrice,
                    UpdateTime = q.UpdateTime,
                    ProductExpiryHours = p?.ExpiryHours,
                    ShelfLifeHours = q.ShelfLifeHours
                };
            }).ToList();

            return ApiResponse<List<SupplierProductQuoteDto>>.Success(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail($"查询供货价失败：{ex.Message}");
        }
    }

    /// <summary>供应商门户：全部上架产品的报价面板，任何供应商可对任何产品报价</summary>
    public async Task<ApiResponse<List<SupplierProductQuoteDto>>> GetAllProductQuotesForSupplierAsync(string supplierId)
    {
        try
        {
            var products = await _productRepo.GetAllAsync();
            var quotes = await _priceRepo.GetQuotesBySupplierAsync(supplierId);
            var quoteMap = quotes.ToDictionary(q => q.ProductID);

            var list = products
                .Where(p => p.Status == "ACTIVE")
                .Select(p =>
                {
                    quoteMap.TryGetValue(p.ProductID, out var q);
                    return new SupplierProductQuoteDto
                    {
                        ProductID = p.ProductID,
                        ProductName = p.ProductName,
                        SupplyPrice = q?.SupplyPrice,
                        UpdateTime = q?.UpdateTime,
                        ProductExpiryHours = p.ExpiryHours,
                        ShelfLifeHours = q?.ShelfLifeHours
                    };
                }).ToList();

            return ApiResponse<List<SupplierProductQuoteDto>>.Success(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail($"查询报价面板失败：{ex.Message}");
        }
    }

    /// <summary>设置/更新某供应商对某产品的供货价（已有报价则更新）</summary>
    public async Task<ApiResponse> SetSupplyPriceAsync(string supplierId, string productId, decimal supplyPrice, int? shelfLifeHours = null)
    {
        if (supplyPrice <= 0)
            return ApiResponse.Fail("供货价必须大于 0");
        if (shelfLifeHours.HasValue && shelfLifeHours.Value <= 0)
            return ApiResponse.Fail("保质期必须大于 0 小时");

        try
        {
            var supplier = await _repo.GetByIdAsync(supplierId);
            if (supplier == null) return ApiResponse.Fail("供应商不存在", 404);

            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null) return ApiResponse.Fail("产品不存在", 404);
            // 任何供应商都可对任何产品报价：报价关系即代表“该供应商供应该产品”

            var quote = await _priceRepo.GetQuoteAsync(supplierId, productId);
            if (quote == null)
            {
                await _priceRepo.AddAsync(new InvSupplierPrice
                {
                    SupplierID = supplierId,
                    ProductID = productId,
                    SupplyPrice = supplyPrice,
                    ShelfLifeHours = shelfLifeHours,
                    UpdateTime = DateTime.Now
                });
            }
            else
            {
                quote.SupplyPrice = supplyPrice;
                quote.ShelfLifeHours = shelfLifeHours;
                quote.UpdateTime = DateTime.Now;
                _priceRepo.Update(quote);
            }

            var shelfDays = (shelfLifeHours ?? product.ExpiryHours) is int h ? $"{h} 小时（{h / 24.0:0.#} 天）" : "未设置";
            return ApiResponse.Success($"已设置 {product.ProductName} 的供货价：¥{supplyPrice:F2}，保质期：{shelfDays}");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"设置供货价失败：{ex.Message}");
        }
    }

    // ========== 供应商登录（供应商门户用）==========

    /// <summary>密码 MD5 哈希（库中已有供应商密码即为此格式）</summary>
    private static string HashPassword(string password)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));

    public async Task<ApiResponse<SupplierDto>> SupplierLoginAsync(string loginAccount, string password)
    {
        if (string.IsNullOrWhiteSpace(loginAccount) || string.IsNullOrWhiteSpace(password))
            return ApiResponse<SupplierDto>.Fail("请输入账号和密码");

        try
        {
            var allSuppliers = await _repo.GetAllAsync();
            var supplier = allSuppliers.FirstOrDefault(s => s.LoginAccount == loginAccount);
            if (supplier == null)
                return ApiResponse<SupplierDto>.Fail("账号不存在", 404);

            var inputHash = HashPassword(password);
            if (!string.Equals(supplier.LoginPassword, inputHash, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<SupplierDto>.Fail("密码错误");

            // 状态拦截：待审核/已禁用/被驳回的供应商不允许登录
            if (supplier.Status == "Pending")
                return ApiResponse<SupplierDto>.Fail("入驻申请正在审核中，请耐心等待");
            if (supplier.Status == "Rejected")
                return ApiResponse<SupplierDto>.Fail("入驻申请已被驳回，请联系平台管理员");
            if (supplier.Status != "Active")
                return ApiResponse<SupplierDto>.Fail("账号已被禁用，请联系平台管理员");

            return ApiResponse<SupplierDto>.Success(MapToDto(supplier), "登录成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<SupplierDto>.Fail($"登录失败：{ex.Message}");
        }
    }

    // ========== 跨组接口（供 C 组调用）==========

    public async Task<ApiResponse<List<SupplierAccountDto>>> FindSupplierAccountAsync(
        string? supplierId = null,
        string? supplierName = null,
        string? loginAccount = null,
        string? contactPhone = null)
    {
        var allSuppliers = await _repo.GetAllAsync();

        var result = allSuppliers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(supplierId))
            result = result.Where(s => s.SupplierID == supplierId);
        if (!string.IsNullOrWhiteSpace(supplierName))
            result = result.Where(s => s.SupplierName.Contains(supplierName));
        if (!string.IsNullOrWhiteSpace(loginAccount))
            result = result.Where(s => s.LoginAccount == loginAccount);
        if (!string.IsNullOrWhiteSpace(contactPhone))
            result = result.Where(s => s.ContactPhone == contactPhone);

        var list = result.Select(s => new SupplierAccountDto
        {
            SupplierID = s.SupplierID,
            SupplierName = s.SupplierName,
            LicenseNo = s.LicenseNo,
            ExpiryDate = s.ExpiryDate,
            CreditLevel = s.CreditLevel,
            ContactPhone = s.ContactPhone,
            LoginAccount = s.LoginAccount
        }).ToList();

        return ApiResponse<List<SupplierAccountDto>>.Success(list);
    }

    public async Task<ApiResponse<bool>> VerifySupplierPasswordAsync(string loginAccount, string password)
    {
        var allSuppliers = await _repo.GetAllAsync();
        var supplier = allSuppliers.FirstOrDefault(s => s.LoginAccount == loginAccount);

        if (supplier == null)
            return ApiResponse<bool>.Fail("账号不存在", 404);

        // 库中密码为 MD5 哈希，比对时同样先哈希输入
        var valid = string.Equals(supplier.LoginPassword, HashPassword(password), StringComparison.OrdinalIgnoreCase);
        return ApiResponse<bool>.Success(valid, valid ? "验证通过" : "密码错误");
    }

    // ========== 商品上架搜索（C 组团长“商品上架”模块用）==========

    /// <summary>
    /// 搜索供应商提供的商品：按供应商（名称/ID）或商品名称（两种命中合并去重）。
    /// 供应商的“可提供商品”由其报价记录（Inv_SupplierPrices）决定：报价即供货关系。
    /// </summary>
    public async Task<ApiResponse<List<SupplierProductEntryDto>>> SearchSupplierProductEntriesAsync(string? keyword)
    {
        try
        {
            var kw = keyword?.Trim();
            if (string.IsNullOrEmpty(kw))
                return ApiResponse<List<SupplierProductEntryDto>>.Success(new List<SupplierProductEntryDto>());

            // 全量数据源（演示/中小规模可直接内存过滤，避免多次连库）
            var products = (await _productRepo.GetAllAsync())
                .Where(p => p.Status == "ACTIVE")
                .ToList();
            var productMap = products.ToDictionary(p => p.ProductID);

            var suppliers = (await _repo.GetAllAsync())
                .Where(s => s.Status == "Active")
                .ToList();
            var supplierMap = suppliers.ToDictionary(s => s.SupplierID);

            var entries = new List<SupplierProductEntryDto>();
            var seen = new HashSet<string>();

            void CollectQuote(InvSupplierPrice q)
            {
                if (!productMap.TryGetValue(q.ProductID, out var p)) return;
                if (!supplierMap.TryGetValue(q.SupplierID, out var s)) return;

                var key = $"{q.SupplierID}|{q.ProductID}";
                if (!seen.Add(key)) return; // 去重：同一（供应商，商品）只保留一次

                entries.Add(new SupplierProductEntryDto
                {
                    SupplierID = s.SupplierID,
                    SupplierName = s.SupplierName,
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Unit = p.Unit,
                    SupplyPrice = q.SupplyPrice,
                    DefaultPrice = p.DefaultPrice,
                    ExpiryHours = q.ShelfLifeHours ?? p.ExpiryHours
                });
            }

            // ① 按供应商命中：供应商名称包含关键词，或供应商ID精确匹配 → 该供应商提供的全部商品
            var matchedSuppliers = suppliers
                .Where(s => s.SupplierID.Equals(kw, StringComparison.OrdinalIgnoreCase)
                            || s.SupplierName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var s in matchedSuppliers)
            {
                var quotes = await _priceRepo.GetQuotesBySupplierAsync(s.SupplierID);
                foreach (var q in quotes) CollectQuote(q);
            }

            // ② 按商品命中：商品名称包含关键词 → 所有供货该商品的供应商
            var matchedProducts = products
                .Where(p => p.ProductName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var p in matchedProducts)
            {
                var quotes = await _priceRepo.GetQuotesByProductWithSupplierAsync(p.ProductID);
                foreach (var q in quotes) CollectQuote(q);
            }

            return ApiResponse<List<SupplierProductEntryDto>>.Success(entries);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SupplierProductEntryDto>>.Fail($"商品搜索失败：{ex.Message}");
        }
    }

    // ========== 管理端（管理员角色管理用）==========

    /// <summary>全部供应商列表（含状态），供管理员启禁用管理</summary>
    public async Task<ApiResponse<List<SupplierDto>>> GetAllSuppliersAsync()
    {
        var all = await _repo.GetAllAsync();
        return ApiResponse<List<SupplierDto>>.Success(all.Select(MapToDto).ToList());
    }

    /// <summary>按状态查询供应商（如 Pending 待审核列表）</summary>
    public async Task<ApiResponse<List<SupplierDto>>> GetSuppliersByStatusAsync(string status)
    {
        var all = await _repo.GetAllAsync();
        var list = all.Where(s => s.Status == status).Select(MapToDto).ToList();
        return ApiResponse<List<SupplierDto>>.Success(list);
    }

    /// <summary>变更供应商状态，含状态流转校验（Active/Pending/Disabled/Rejected）</summary>
    public async Task<ApiResponse> SetSupplierStatusAsync(string supplierId, string targetStatus)
    {
        var allowedTargets = new[] { "Active", "Pending", "Disabled", "Rejected" };
        if (!allowedTargets.Contains(targetStatus))
            return ApiResponse.Fail("非法的目标状态");

        try
        {
            var supplier = await _repo.GetByIdAsync(supplierId);
            if (supplier == null)
                return ApiResponse.Fail("供应商不存在", 404);
            if (supplier.Status == targetStatus)
                return ApiResponse.Fail("供应商已处于该状态，无需变更");

            // 状态流转约束：待审核只能 通过(Active)/驳回(Rejected)；已禁用/已驳回只能重新启用(Active)；正常只能禁用
            var allowed = supplier.Status switch
            {
                "Pending" => new[] { "Active", "Rejected" },
                "Active" => new[] { "Disabled" },
                "Disabled" or "Rejected" => new[] { "Active" },
                _ => Array.Empty<string>()
            };
            if (!allowed.Contains(targetStatus))
                return ApiResponse.Fail($"不允许从 {supplier.Status} 变更为 {targetStatus}");

            supplier.Status = targetStatus;
            _repo.Update(supplier);
            return ApiResponse.Success("状态已更新");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"状态更新失败：{ex.Message}");
        }
    }

    private static SupplierDto MapToDto(InvSupplier s) => new()
    {
        SupplierID = s.SupplierID, SupplierName = s.SupplierName,
        LicenseNo = s.LicenseNo, ExpiryDate = s.ExpiryDate,
        CreditLevel = s.CreditLevel, ContactPhone = s.ContactPhone,
        LoginAccount = s.LoginAccount,
        Status = s.Status,
        // SQL 聚合查出来的产品数优先（列表页）；否则用已加载的 Products（详情页）
        ProductCount = s.ProductCount > 0 ? s.ProductCount : (s.Products?.Count ?? 0)
    };
}
