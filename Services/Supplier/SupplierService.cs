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
            var imageMap = await LoadProductImageMapAsync(supplierId);

            var list = quotes.Select(q =>
            {
                productMap.TryGetValue(q.ProductID, out var p);
                return BuildQuoteDto(q.ProductID, p?.ProductName ?? q.ProductID, q.SupplyPrice, q.UpdateTime,
                    p?.ExpiryHours, q.ShelfLifeHours, q.Description ?? p?.Description, imageMap);
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
            var imageMap = await LoadProductImageMapAsync(supplierId);

            var list = products
                .Select(p =>
                {
                    quoteMap.TryGetValue(p.ProductID, out var q);
                    return BuildQuoteDto(p.ProductID, p.ProductName, q?.SupplyPrice, q?.UpdateTime,
                        p.ExpiryHours, q?.ShelfLifeHours, q?.Description ?? p.Description, imageMap);
                }).ToList();

            return ApiResponse<List<SupplierProductQuoteDto>>.Success(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SupplierProductQuoteDto>>.Fail($"查询报价面板失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierProductQuoteDto>> GetProductInfoForSupplierAsync(string supplierId, string productId)
    {
        try
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null) return ApiResponse<SupplierProductQuoteDto>.Fail("产品不存在", 404);

            var quote = await _priceRepo.GetQuoteAsync(supplierId, productId);
            if (quote == null) return ApiResponse<SupplierProductQuoteDto>.Fail("请先对该商品报价，报价后即可维护商品图文");

            // 该商品的图片：自己上传的在前，平台通用图在后（编辑页展示全部，含无数据旧记录供清理）
            var images = (await _productRepo.GetProductImagesAsync(productId))
                .OrderBy(i => i.SupplierID != supplierId) // 自己上传的排前面
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.CreateTime)
                .Select(i => new SupplierProductImageDto
                {
                    ImageID = i.ImageID,
                    ImageUrl = i.ImageUrl,
                    HasImageData = i.HasData,
                    IsOwned = i.SupplierID == supplierId
                })
                .ToList();

            return ApiResponse<SupplierProductQuoteDto>.Success(new SupplierProductQuoteDto
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                SupplyPrice = quote.SupplyPrice,
                UpdateTime = quote.UpdateTime,
                ProductExpiryHours = product.ExpiryHours,
                ShelfLifeHours = quote.ShelfLifeHours,
                Description = quote.Description ?? product.Description,
                Images = images
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<SupplierProductQuoteDto>.Fail($"查询商品信息失败：{ex.Message}");
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

    private static SupplierProductQuoteDto BuildQuoteDto(
        string productId, string productName, decimal? supplyPrice, DateTime? updateTime,
        int? productExpiryHours, int? shelfLifeHours, string? description,
        Dictionary<string, List<SupplierProductImageDto>> imageMap)
    {
        return new SupplierProductQuoteDto
        {
            ProductID = productId,
            ProductName = productName,
            SupplyPrice = supplyPrice,
            UpdateTime = updateTime,
            ProductExpiryHours = productExpiryHours,
            ShelfLifeHours = shelfLifeHours,
            Description = description,
            Images = imageMap.TryGetValue(productId, out var urls) ? urls : new List<SupplierProductImageDto>()
        };
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

    // ========== 供应商维护商品图文（文字介绍 + 图片，报价即供货关系）==========

    /// <summary>校验供应商存在且已对该商品报价（报价即供货关系），返回商品；不满足返回 null</summary>
    private async Task<(InvSupplier? Supplier, InvProduct? Product, InvSupplierPrice? Quote)> ResolveSuppliedProductAsync(string supplierId, string productId)
    {
        var supplier = await _repo.GetByIdAsync(supplierId);
        if (supplier == null) return (null, null, null);

        var product = await _productRepo.GetByIdAsync(productId);
        if (product == null) return (supplier, null, null);

        var quote = await _priceRepo.GetQuoteAsync(supplierId, productId);
        return (supplier, product, quote);
    }

    public async Task<ApiResponse> UpdateProductDescriptionAsync(string supplierId, string productId, string? description)
    {
        var (supplier, product, quote) = await ResolveSuppliedProductAsync(supplierId, productId);
        if (supplier == null) return ApiResponse.Fail("供应商不存在", 404);
        if (product == null) return ApiResponse.Fail("产品不存在", 404);
        if (quote == null) return ApiResponse.Fail("请先对该商品报价，报价后即可维护商品图文");

        description = description?.Trim();
        if (!string.IsNullOrEmpty(description) && description.Length > 2000)
            return ApiResponse.Fail("商品介绍不能超过 2000 字");

        try
        {
            // 简介归属（供应商×商品）组合：写在报价记录上，各家供应商互不影响
            quote.Description = string.IsNullOrEmpty(description) ? null : description;
            _priceRepo.Update(quote);
            return ApiResponse.Success("商品文字介绍已保存");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"保存商品介绍失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse> AddProductImageAsync(string supplierId, string productId, byte[] imageData, string imageType)
    {
        if (imageData == null || imageData.Length == 0)
            return ApiResponse.Fail("图片数据不能为空");

        var (supplier, product, quote) = await ResolveSuppliedProductAsync(supplierId, productId);
        if (supplier == null) return ApiResponse.Fail("供应商不存在", 404);
        if (product == null) return ApiResponse.Fail("产品不存在", 404);
        if (quote == null) return ApiResponse.Fail("请先对该商品报价，报价后即可维护商品图文");

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
                .ToList();
            var productMap = products.ToDictionary(p => p.ProductID);

            var suppliers = (await _repo.GetAllAsync())
                .Where(s => s.Status == "Active")
                .ToList();
            var supplierMap = suppliers.ToDictionary(s => s.SupplierID);

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
                    DefaultPrice = q.SupplyPrice,
                    ExpiryHours = q.ShelfLifeHours ?? p.ExpiryHours,
                    Description = q.Description ?? p.Description, // 该供应商的简介，未写时兜底商品通用介绍
                    Images = GetDisplayImages(p.ProductID, q.SupplierID)
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
