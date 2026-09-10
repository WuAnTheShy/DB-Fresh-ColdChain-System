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
    private readonly IProductRepository _productRepo;
    private readonly IGoodsRepository _goodsRepo;
    // 供应商动态定价引擎：团长端商品上架搜索按规则引擎展示动态报价
    private readonly IPricingService _pricing;

    public SupplierService(ISupplierRepository repo, IProductRepository productRepo, IGoodsRepository goodsRepo, IPricingService pricing)
    {
        _repo = repo;
        _productRepo = productRepo;
        _goodsRepo = goodsRepo;
        _pricing = pricing;
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
                LoginPassword = string.IsNullOrEmpty(dto.LoginPassword) ? null : HashPassword(dto.LoginPassword),
                // 管理员代建与后台登记均直接生效；自助入驻若走此接口也按已审核处理
                Status = "Active"
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

    // ========== 供货价（进价由供应商决定，统一以「我的货物」售价为准）==========

    /// <summary>供应商详情页用：该供应商已建立货物的所有商品（多供应商模式下，货物即供货关系）</summary>
    public async Task<ApiResponse<List<SupplierProductQuoteDto>>> GetSupplierProductQuotesAsync(string supplierId)
    {
        try
        {
            var products = await _productRepo.GetAllAsync();
            var productMap = products.ToDictionary(p => p.ProductID);
            var goods = await _goodsRepo.GetBySupplierAsync(supplierId);
            var imageMap = await LoadProductImageMapAsync(supplierId);

            var list = goods.Select(g =>
            {
                productMap.TryGetValue(g.ProductID, out var p);
                return new SupplierProductQuoteDto
                {
                    ProductID = g.ProductID,
                    ProductName = p?.ProductName ?? g.ProductID,
                    SupplyPrice = g.SalePrice,
                    UpdateTime = g.UpdateTime,
                    ProductExpiryHours = p?.ExpiryHours,
                    ShelfLifeHours = g.ShelfLifeHours,
                    Description = !string.IsNullOrWhiteSpace(g.Description) ? g.Description : p?.Description,
                    Images = imageMap.TryGetValue(g.ProductID, out var urls) ? urls : new List<SupplierProductImageDto>()
                };
            }).ToList();

            return ApiResponse<List<SupplierProductQuoteDto>>.Success(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail($"查询供货商品失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 加载商品图片并按商品分组：该供应商自己上传的在前，平台通用图（SupplierID 为空）在后，
    /// 每商品最多取前 3 张用于展示。
    /// </summary>
    private async Task<Dictionary<string, List<SupplierProductImageDto>>> LoadProductImageMapAsync(string supplierId)
    {
        return (await _productRepo.GetAllProductImagesAsync())
            .Where(img => img.HasData)
            .GroupBy(img => img.ProductID)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(img => img.SupplierID != supplierId) // 自己的图优先
                      .ThenBy(img => img.SortOrder)
                      .ThenBy(img => img.CreateTime)
                      .Take(3)
                      .Select(img => new SupplierProductImageDto
                      {
                          ImageID = img.ImageID,
                          ImageUrl = img.ImageUrl,
                          HasImageData = img.HasData,
                          IsOwned = img.SupplierID == supplierId
                      })
                      .ToList());
    }

    // ========== 供应商维护商品图文（文字介绍 + 图片，前提是已对该物品建立货物）==========

    /// <summary>供应商对该物品是否已建立货物；未建立返回 null（货物即供货关系）</summary>
    private async Task<InvGoods?> ResolveSuppliedProductAsync(string supplierId, string productId)
    {
        return await _goodsRepo.GetAsync(productId, supplierId);
    }

    public async Task<ApiResponse> AddProductImageAsync(string supplierId, string productId, byte[] imageData, string imageType)
    {
        if (imageData == null || imageData.Length == 0)
            return ApiResponse.Fail("图片数据不能为空");

        var goods = await ResolveSuppliedProductAsync(supplierId, productId);
        if (goods == null) return ApiResponse.Fail("请先在「我的货物」对该物品建立货物，之后即可维护商品图文");

        try
        {
            var existing = await _productRepo.GetProductImagesAsync(productId);
            const int maxImages = 9; // 一个商品最多 9 张图片（对外展示按 SortOrder 取前 3 张）
            if (existing.Count >= maxImages)
                return ApiResponse.Fail($"每个商品最多 {maxImages} 张图片，请先删除部分图片");

            var imageId = Guid.NewGuid().ToString();
            var nextSortOrder = existing.Count == 0 ? 1 : existing.Max(i => i.SortOrder) + 1;
            await _productRepo.AddProductImageAsync(new InvProductImage
            {
                ImageID = imageId,
                ProductID = productId,
                SupplierID = supplierId, // 图片归属该供应商，其他供应商不可见/不可删
                // 图片本体存 BLOB，对外地址统一走 /images/product/{ImageID} 接口
                ImageUrl = $"/images/product/{imageId}",
                ImageData = imageData,
                ImageType = imageType,
                SortOrder = nextSortOrder,
                CreateTime = DateTime.Now
            });

            return ApiResponse.Success("商品图片已上传");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"上传商品图片失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<ProductImageContentDto>> GetProductImageContentAsync(string imageId)
    {
        try
        {
            var result = await _productRepo.GetProductImageDataAsync(imageId);
            if (result == null || result.Value.Data == null || result.Value.Data.Length == 0)
                return ApiResponse<ProductImageContentDto>.Fail("图片不存在", 404);

            return ApiResponse<ProductImageContentDto>.Success(new ProductImageContentDto
            {
                Data = result.Value.Data,
                ContentType = string.IsNullOrWhiteSpace(result.Value.ContentType)
                    ? "image/jpeg"
                    : result.Value.ContentType
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductImageContentDto>.Fail($"读取商品图片失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> DeleteProductImageAsync(string supplierId, string imageId)
    {
        try
        {
            var image = await _productRepo.GetProductImageByIdAsync(imageId);
            if (image == null) return ApiResponse<string>.Fail("图片不存在", 404);

            // 只能删除自己上传的图片；平台通用图（SupplierID 为空）所有供应商不可删
            if (image.SupplierID != supplierId)
                return ApiResponse<string>.Fail("只能删除自己上传的图片");

            await _productRepo.DeleteProductImageAsync(imageId);
            return ApiResponse<string>.Success(image.ImageUrl, "商品图片已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"删除商品图片失败：{ex.Message}");
        }
    }

    // ========== 供应商登录（供应商门户用）==========

    /// <summary>密码 MD5 哈希（库中已有供应商密码即为此格式）</summary>
    private static string HashPassword(string password)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));

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
            LoginAccount = s.LoginAccount,
            Status = s.Status?.Trim() ?? string.Empty
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
    /// 供应商的“可提供商品”由其货物记录（Inv_Goods）决定：货物即该供应商的上架供货，
    /// 售价、保质期与上下架状态均以“我的货物”中维护的数据为准。
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
                .ToList();
            var productMap = products.ToDictionary(p => p.ProductID, StringComparer.OrdinalIgnoreCase);

            // 供应商：仅正常（Active）状态可被搜索到
            var suppliers = (await _repo.GetAllAsync())
                .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var supplierMap = suppliers.ToDictionary(s => s.SupplierID, StringComparer.OrdinalIgnoreCase);

            // 货物 = 供应商上架供货（唯一数据源）
            var allGoods = (await _goodsRepo.GetAllAsync()).ToList();

            // 全部商品图片（仅取真正有二进制数据的）；每条（供应商×商品）组合：
            // 该供应商自己上传的图在前，平台通用图（SupplierID 为空）在后，最多 3 张
            var allImages = (await _productRepo.GetAllProductImagesAsync())
                .Where(img => img.HasData)
                .ToList();

            List<string> GetDisplayImages(string productId, string sid) =>
                allImages
                    .Where(img => img.ProductID == productId && (img.SupplierID == sid || img.SupplierID == null))
                    .OrderBy(img => img.SupplierID != sid)
                    .ThenBy(img => img.SortOrder)
                    .ThenBy(img => img.CreateTime)
                    .Take(3)
                    .Select(img => img.ImageUrl)
                    .ToList();

            var entries = new List<SupplierProductEntryDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void CollectEntry(InvProduct p, InvSupplier s, InvGoods goods)
            {
                var key = $"{s.SupplierID}|{p.ProductID}";
                if (!seen.Add(key)) return; // 去重：同一（供应商，商品）只保留一次

                var salePrice = goods.SalePrice;
                var description = !string.IsNullOrWhiteSpace(goods.Description) ? goods.Description : p.Description;

                entries.Add(new SupplierProductEntryDto
                {
                    SupplierID = s.SupplierID,
                    SupplierName = s.SupplierName,
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Unit = p.Unit,
                    SupplyPrice = salePrice, // 先以货物售价兜底，下方统一按规则引擎覆盖为动态报价
                    DefaultPrice = Math.Round(salePrice * 1.2m, 2), // 兜底推荐价=供货价×1.2（动态覆盖后同样×1.2）
                    ExpiryHours = goods.ShelfLifeHours ?? p.ExpiryHours,
                    // 供应商上架（货物）时填写的文字介绍，团长可参考/复制/改写；未写时兜底商品通用介绍
                    Description = description,
                    Images = GetDisplayImages(p.ProductID, s.SupplierID),
                    // 上架状态取该供应商的货物状态（ACTIVE=上架，其它=下架）
                    ProductStatus = goods.Status
                });
            }

            // ① 按供应商命中：供应商名称包含关键词，或供应商ID精确匹配 → 该供应商提供的全部商品
            var matchedSuppliers = suppliers
                .Where(s => s.SupplierID.Equals(kw, StringComparison.OrdinalIgnoreCase)
                            || s.SupplierName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var s in matchedSuppliers)
            {
                foreach (var g in allGoods.Where(g => g.SupplierID == s.SupplierID))
                {
                    if (!productMap.TryGetValue(g.ProductID, out var p)) continue;
                    CollectEntry(p, s, g);
                }
            }

            // ② 按商品命中：商品名称包含关键词 → 所有供货该商品的（供应商×商品）
            var matchedProducts = products
                .Where(p => p.ProductName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var p in matchedProducts)
            {
                foreach (var g in allGoods.Where(g => g.ProductID == p.ProductID))
                {
                    if (!supplierMap.TryGetValue(g.SupplierID, out var s)) continue;
                    CollectEntry(p, s, g);
                }
            }

            // 报价 = 供应商动态定价：逐条按规则引擎实时计算（商品×供应商×数量1×当前时间）；
            // 推荐价 = 动态报价 × 1.2（倍率不变）。此页为“入团前预览”，
            // 真正入团时服务层会以当时价格再次计算并快照落库（CRM_PRODUCT_ENTRIES.SUPPLYPRICE/DEFAULTPRICE）。
            foreach (var entry in entries)
            {
                try
                {
                    var calc = await _pricing.CalculatePriceAsync(new PriceCalculationRequest
                    {
                        ProductID = entry.ProductID,
                        SupplierID = entry.SupplierID,
                        Quantity = 1m
                    });
                    if (calc.IsSuccess && calc.Data != null)
                    {
                        entry.SupplyPrice = calc.Data.FinalPrice;
                        entry.DefaultPrice = Math.Round(entry.SupplyPrice * 1.2m, 2);
                    }
                }
                catch
                {
                    // 规则引擎异常时保留货物售价兜底展示，避免搜索失败
                }
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
        var goods = await _goodsRepo.GetAllAsync();
        var namesBySupplier = goods
            .Where(g => !string.IsNullOrWhiteSpace(g.SupplierID) && !string.IsNullOrWhiteSpace(g.Product?.ProductName))
            .GroupBy(g => g.SupplierID)
            .ToDictionary(g => g.Key, g => g.Select(item => item.Product!.ProductName).Distinct().ToList());

        var list = all.Select(s =>
        {
            var dto = MapToDto(s);
            dto.ProductNames = namesBySupplier.GetValueOrDefault(s.SupplierID) ?? new List<string>();
            dto.ProductCount = dto.ProductNames.Count;
            return dto;
        }).ToList();

        return ApiResponse<List<SupplierDto>>.Success(list);
    }

    /// <summary>按状态查询供应商（如 Pending 待审核列表）</summary>
    public async Task<ApiResponse<List<SupplierDto>>> GetSuppliersByStatusAsync(string status)
    {
        var all = await _repo.GetAllAsync();
        var list = all.Where(s => string.Equals(s.Status?.Trim(), status, StringComparison.OrdinalIgnoreCase))
            .Select(MapToDto).ToList();
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
        Status = s.Status?.Trim() ?? string.Empty,
        ProductCount = s.ProductCount > 0 ? s.ProductCount : (s.Products?.Count ?? 0),
        ProductNames = s.Products?.Select(p => p.ProductName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList() ?? new()
    };
}
