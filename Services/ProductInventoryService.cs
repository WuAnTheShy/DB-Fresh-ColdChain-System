using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

public class ProductInventoryService : IProductInventoryService
{
    private readonly IProductRepository _productRepo;
    private readonly IStockSummaryRepository _stockRepo;
    private readonly IStockBatchRepository _batchRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ISupplierPriceRepository _supplierPriceRepo;
    private readonly IUnitOfWork _uow;

    public ProductInventoryService(
        IProductRepository productRepo,
        IStockSummaryRepository stockRepo,
        IStockBatchRepository batchRepo,
        ICategoryRepository categoryRepo,
        ISupplierPriceRepository supplierPriceRepo,
        IUnitOfWork uow)
    {
        _productRepo = productRepo;
        _stockRepo = stockRepo;
        _batchRepo = batchRepo;
        _categoryRepo = categoryRepo;
        _supplierPriceRepo = supplierPriceRepo;
        _uow = uow;
    }

    // ========== 产品 ==========

    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var (items, total) = await _productRepo.GetPagedWithDetailsAsync(pageIndex, pageSize, keyword);
        return ApiResponse<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>
        {
            PageIndex = pageIndex, PageSize = pageSize, TotalCount = total,
            Items = items.Select(MapToDto).ToList()
        });
    }

    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(string id)
    {
        var p = await _productRepo.GetByIdWithDetailsAsync(id);
        if (p == null) return ApiResponse<ProductDto>.Fail("产品不存在", 404);
        return ApiResponse<ProductDto>.Success(MapToDto(p));
    }

    public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto)
    {
        var product = new InvProduct
        {
            ProductName = dto.ProductName, CategoryID = dto.CategoryID,
            SupplierID = dto.SupplierID, Unit = dto.Unit,
            WeightKG = dto.WeightKG, VolumeLitre = dto.VolumeLitre,
            ExpiryHours = dto.ExpiryHours, StorageReq = dto.StorageReq,
            DefaultPrice = dto.DefaultPrice, Status = "ACTIVE"
        };

        try
        {
            await _uow.BeginAsync();
            await _productRepo.AddAsync(product);

            var summary = new InvStockSummary
            {
                ProductID = product.ProductID,
                TotalQty = dto.InitialStock,
                LockedQty = 0,
                AvailableQty = dto.InitialStock
            };
            await _stockRepo.AddAsync(summary);
            product.StockSummary = summary;

            await _uow.CommitAsync();
            return ApiResponse<ProductDto>.Success(MapToDto(product), "产品创建成功");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse<ProductDto>.Fail($"产品创建失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(string id, UpdateProductDto dto)
    {
        var p = await _productRepo.GetByIdWithDetailsAsync(id);
        if (p == null) return ApiResponse<ProductDto>.Fail("产品不存在", 404);

        if (dto.ProductName != null) p.ProductName = dto.ProductName;
        if (dto.CategoryID != null) p.CategoryID = dto.CategoryID;
        if (dto.Unit != null) p.Unit = dto.Unit;
        if (dto.WeightKG.HasValue) p.WeightKG = dto.WeightKG;
        if (dto.VolumeLitre.HasValue) p.VolumeLitre = dto.VolumeLitre;
        if (dto.ExpiryHours.HasValue) p.ExpiryHours = dto.ExpiryHours;
        if (dto.StorageReq != null) p.StorageReq = dto.StorageReq;
        if (dto.DefaultPrice.HasValue) p.DefaultPrice = dto.DefaultPrice.Value;
        if (dto.Status != null) p.Status = dto.Status;

        _productRepo.Update(p);
        return ApiResponse<ProductDto>.Success(MapToDto(p), "产品更新成功");
    }

    public async Task<ApiResponse> DeleteProductAsync(string id)
    {
        var p = await _productRepo.GetByIdAsync(id);
        if (p == null) return ApiResponse.Fail("产品不存在", 404);
        p.Status = "INACTIVE";
        _productRepo.Update(p);
        return ApiResponse.Success("产品已下架");
    }

    // ========== 跨组接口（供 C 组调用）==========

    public async Task<ApiResponse<int>> GetProductStockAsync(string productId)
    {
        var st = await _stockRepo.GetByProductIdAsync(productId);
        return st == null
            ? ApiResponse<int>.Fail("产品库存记录不存在", 404)
            : ApiResponse<int>.Success(st.AvailableQty);
    }

    // ========== 分类 ==========

    public async Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync()
    {
        var list = await _categoryRepo.GetAllAsync();
        return ApiResponse<List<CategoryDto>>.Success(list.Select(c => new CategoryDto
        {
            CategoryID = c.CategoryID, CategoryName = c.CategoryName, ParentID = c.ParentID
        }).ToList());
    }

    public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var c = new InvCategory { CategoryName = dto.CategoryName, ParentID = dto.ParentID };
        await _categoryRepo.AddAsync(c);
        return ApiResponse<CategoryDto>.Success(new CategoryDto
        {
            CategoryID = c.CategoryID, CategoryName = c.CategoryName, ParentID = c.ParentID
        }, "分类创建成功");
    }

    // ========== 库存 ==========

    public async Task<ApiResponse<InventoryDto>> GetInventoryAsync(string productId)
    {
        var st = await _stockRepo.GetByProductIdAsync(productId);
        if (st == null) return ApiResponse<InventoryDto>.Fail("库存记录不存在", 404);
        return ApiResponse<InventoryDto>.Success(new InventoryDto
        {
            StockID = st.StockID, ProductID = st.ProductID,
            ProductName = st.Product?.ProductName ?? "",
            TotalQty = st.TotalQty, LockedQty = st.LockedQty,
            AvailableQty = st.AvailableQty, UpdateTime = st.UpdateTime
        });
    }

    public async Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10)
    {
        var list = await _stockRepo.GetLowStockAsync(threshold);
        return ApiResponse<List<InventoryDto>>.Success(list.Select(st => new InventoryDto
        {
            StockID = st.StockID, ProductID = st.ProductID,
            ProductName = st.Product?.ProductName ?? "",
            TotalQty = st.TotalQty, LockedQty = st.LockedQty,
            AvailableQty = st.AvailableQty, UpdateTime = st.UpdateTime
        }).ToList());
    }

    public async Task<ApiResponse> StockInAsync(UpdateInventoryDto dto, string? batchNo = null, DateTime? productionDate = null, DateTime? expiryDate = null)
    {
        if (dto.Quantity <= 0)
            return ApiResponse.Fail("入库数量必须大于 0");

        try
        {
            // 进价由供应商决定：入库时自动取该供应商对该产品的供货价，操作员不可手填
            var product = await _productRepo.GetByIdAsync(dto.ProductID);
            if (product == null)
                return ApiResponse.Fail("产品不存在", 404);
            if (string.IsNullOrWhiteSpace(product.SupplierID))
                return ApiResponse.Fail("该产品未关联供应商，无法入库");
            var quote = await _supplierPriceRepo.GetQuoteAsync(product.SupplierID, dto.ProductID);
            if (quote == null)
                return ApiResponse.Fail("该供应商尚未对此产品报价，请先在供应商详情页设置供货价");

            await _uow.BeginAsync();

            var st = await _stockRepo.GetByProductIdForUpdateAsync(dto.ProductID);
            if (st == null)
            {
                st = new InvStockSummary
                {
                    ProductID = dto.ProductID,
                    TotalQty = dto.Quantity, LockedQty = 0, AvailableQty = dto.Quantity
                };
                await _stockRepo.AddAsync(st);
            }
            else
            {
                st.TotalQty += dto.Quantity;
                st.AvailableQty = st.TotalQty - st.LockedQty;
                st.UpdateTime = DateTime.Now;
                _stockRepo.Update(st);
            }

            // 创建批次记录（批次号未填则按 BAT年月日-序号 自动生成）
            if (string.IsNullOrWhiteSpace(batchNo))
            {
                var prefix = $"BAT{DateTime.Now:yyyyMMdd}";
                var maxNo = await _batchRepo.GetMaxBatchNoByPrefixAsync(prefix);
                batchNo = $"{prefix}-{(maxNo + 1):D2}";
            }

            var batch = new InvStockBatch
            {
                ProductID = dto.ProductID,
                SupplierID = product.SupplierID,
                BatchNo = batchNo,
                InPrice = quote.SupplyPrice,
                ProductionDate = productionDate,
                ExpiryDate = expiryDate,
                InitialQty = dto.Quantity,
                CurrentQty = dto.Quantity,
                Status = "ACTIVE"
            };
            await _batchRepo.AddAsync(batch);

            await _uow.CommitAsync();
            return ApiResponse.Success($"入库成功，当前库存: {st.TotalQty}");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse.Fail($"入库失败：{ex.Message}");
        }
    }

    public async Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto)
    {
        if (dto.Quantity <= 0)
            return ApiResponse.Fail("出库数量必须大于 0");

        try
        {
            await _uow.BeginAsync();

            // FOR UPDATE 行级锁：阻塞并发请求对同一产品库存的修改
            var st = await _stockRepo.GetByProductIdForUpdateAsync(dto.ProductID);
            if (st == null) { await _uow.RollbackAsync(); return ApiResponse.Fail("库存记录不存在", 404); }

            // FEFO 扣减：先查批次实际可用量（双保险：汇总+批次都校验）
            var remaining = dto.Quantity;
            var batches = await _batchRepo.GetByProductIdForUpdateAsync(dto.ProductID);
            var batchTotal = batches.Sum(b => b.CurrentQty);
            if (batchTotal < dto.Quantity)
            {
                // 汇总数据不准时自动修正：修正必须提交（回滚会撤销修正），故此处提交修正后返回失败
                st.TotalQty = batchTotal;
                st.AvailableQty = st.TotalQty - st.LockedQty;
                st.UpdateTime = DateTime.Now;
                _stockRepo.Update(st);
                await _uow.CommitAsync();
                return ApiResponse.Fail($"实际可用库存不足（批次:{batchTotal}），库存汇总已自动修正");
            }
            if (st.AvailableQty < dto.Quantity) { await _uow.RollbackAsync(); return ApiResponse.Fail("库存不足"); }

            // 逐批次扣减
            foreach (var batch in batches)
            {
                if (remaining <= 0) break;
                var deduct = Math.Min(remaining, batch.CurrentQty);
                if (deduct <= 0) continue;
                batch.CurrentQty -= deduct;
                if (batch.CurrentQty == 0) batch.Status = "DEPLETED";
                _batchRepo.Update(batch);
                remaining -= deduct;
            }

            if (remaining > 0)
                throw new InvalidOperationException($"商品 {dto.ProductID} 可用批次库存不足，还差 {remaining}");

            // 扣减汇总（基于实际扣减量 = dto.Quantity - remaining）
            st.TotalQty -= dto.Quantity;
            st.AvailableQty = st.TotalQty - st.LockedQty;
            st.UpdateTime = DateTime.Now;
            _stockRepo.Update(st);

            await _uow.CommitAsync();
            return ApiResponse.Success($"出库成功，当前库存: {st.TotalQty}");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse.Fail($"出库失败：{ex.Message}");
        }
    }

    // ========== 批次 ==========

    public async Task<ApiResponse<List<StockBatchDto>>> GetBatchesAsync(string productId)
    {
        var list = await _batchRepo.GetByProductIdWithSupplierAsync(productId);
        return ApiResponse<List<StockBatchDto>>.Success(list.Select(b => new StockBatchDto
        {
            BatchID = b.BatchID, ProductID = b.ProductID,
            SupplierID = b.SupplierID, SupplierName = b.Supplier?.SupplierName,
            BatchNo = b.BatchNo, ProductionDate = b.ProductionDate,
            ExpiryDate = b.ExpiryDate, InPrice = b.InPrice,
            InitialQty = b.InitialQty, CurrentQty = b.CurrentQty, Status = b.Status
        }).ToList());
    }

    public async Task MarkExpiredBatchesAsync()
    {
        var count = await _batchRepo.MarkExpiredBatchesAsync();
        if (count == 0) return;

        // 标记后重算所有产品库存汇总
        var allStocks = await _stockRepo.GetAllAsync();
        foreach (var st in allStocks)
        {
            var total = await _batchRepo.GetActiveTotalByProductIdAsync(st.ProductID);
            if (st.TotalQty != total)
            {
                st.TotalQty = total;
                st.AvailableQty = Math.Max(0, st.TotalQty - st.LockedQty);
                st.UpdateTime = DateTime.Now;
                _stockRepo.Update(st);
            }
        }
    }

    public async Task<ApiResponse<StockBatchDto>> AddBatchAsync(CreateStockBatchDto dto)
    {
        if (dto.InitialQty <= 0)
            return ApiResponse<StockBatchDto>.Fail("批次数量必须大于 0");

        try
        {
            // 进价由供应商决定：自动取该供应商对该产品的供货价
            var quote = await _supplierPriceRepo.GetQuoteAsync(dto.SupplierID ?? "", dto.ProductID);
            if (quote == null)
                return ApiResponse<StockBatchDto>.Fail("该供应商尚未对此产品报价，请先在供应商详情页设置供货价");

            await _uow.BeginAsync();

            var batch = new InvStockBatch
            {
                ProductID = dto.ProductID, SupplierID = dto.SupplierID,
                BatchNo = dto.BatchNo, ProductionDate = dto.ProductionDate,
                ExpiryDate = dto.ExpiryDate, InPrice = quote.SupplyPrice,
                InitialQty = dto.InitialQty, CurrentQty = dto.InitialQty,
                Status = "ACTIVE"
            };
            await _batchRepo.AddAsync(batch);

            // 同步更新库存汇总
            var st = await _stockRepo.GetByProductIdForUpdateAsync(dto.ProductID);
            if (st != null)
            {
                st.TotalQty += dto.InitialQty;
                st.AvailableQty = st.TotalQty - st.LockedQty;
                st.UpdateTime = DateTime.Now;
                _stockRepo.Update(st);
            }

            await _uow.CommitAsync();
            return ApiResponse<StockBatchDto>.Success(new StockBatchDto
            {
                BatchID = batch.BatchID, ProductID = batch.ProductID,
                BatchNo = batch.BatchNo, ProductionDate = batch.ProductionDate,
                ExpiryDate = batch.ExpiryDate, InPrice = batch.InPrice,
                InitialQty = batch.InitialQty, CurrentQty = batch.CurrentQty, Status = batch.Status
            }, "批次创建成功");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return ApiResponse<StockBatchDto>.Fail($"批次创建失败：{ex.Message}");
        }
    }

    // ========== 映射 ==========

    private static ProductDto MapToDto(InvProduct p) => new()
    {
        ProductID = p.ProductID, ProductName = p.ProductName,
        CategoryName = p.Category?.CategoryName,
        SupplierName = p.Supplier?.SupplierName,
        Unit = p.Unit, WeightKG = p.WeightKG, VolumeLitre = p.VolumeLitre,
        ExpiryHours = p.ExpiryHours, StorageReq = p.StorageReq,
        DefaultPrice = p.DefaultPrice, Status = p.Status,
        AvailableStock = p.StockSummary?.AvailableQty ?? 0
    };
}
