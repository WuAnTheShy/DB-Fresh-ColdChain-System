using System.Security.Cryptography;
using System.Text;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Logging;

namespace FreshColdChain.Services.Supplier;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repo;
    private readonly ISupplierPriceRepository _priceRepo;
    private readonly IProductRepository _productRepo;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(ISupplierRepository repo, ISupplierPriceRepository priceRepo, IProductRepository productRepo, ILogger<SupplierService> logger)
    {
        _repo = repo;
        _priceRepo = priceRepo;
        _productRepo = productRepo;
        _logger = logger;
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
            _logger.LogError(ex, "供应商创建失败 SupplierName={SupplierName}", dto.SupplierName);
            return ApiResponse<SupplierDto>.Fail("供应商创建失败，请稍后重试");
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
            _logger.LogError(ex, "供应商更新失败 SupplierID={SupplierID}", id);
            return ApiResponse<SupplierDto>.Fail("供应商更新失败，请稍后重试");
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
            _logger.LogError(ex, "供应商删除失败 SupplierID={SupplierID}", id);
            return ApiResponse.Fail("供应商删除失败，请稍后重试");
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
            _logger.LogError(ex, "查询供货价失败 SupplierID={SupplierID}", supplierId);
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail("查询供货价失败，请稍后重试");
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
            _logger.LogError(ex, "查询报价面板失败 SupplierID={SupplierID}", supplierId);
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail("查询报价面板失败，请稍后重试");
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
            _logger.LogError(ex, "设置供货价失败 SupplierID={SupplierID} ProductID={ProductID}", supplierId, productId);
            return ApiResponse.Fail("设置供货价失败，请稍后重试");
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

            return ApiResponse<SupplierDto>.Success(MapToDto(supplier), "登录成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "供应商登录失败 LoginAccount={LoginAccount}", loginAccount);
            return ApiResponse<SupplierDto>.Fail("登录失败，请稍后重试");
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

    private static SupplierDto MapToDto(InvSupplier s) => new()
    {
        SupplierID = s.SupplierID, SupplierName = s.SupplierName,
        LicenseNo = s.LicenseNo, ExpiryDate = s.ExpiryDate,
        CreditLevel = s.CreditLevel, ContactPhone = s.ContactPhone,
        LoginAccount = s.LoginAccount,
        // SQL 聚合查出来的产品数优先（列表页）；否则用已加载的 Products（详情页）
        ProductCount = s.ProductCount > 0 ? s.ProductCount : (s.Products?.Count ?? 0)
    };
}
