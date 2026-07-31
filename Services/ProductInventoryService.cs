using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;
using FreshGroupSystem.Data;

namespace FreshGroupSystem.Services;

public class ProductInventoryService : IProductInventoryService
{
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IUnitOfWork _uow;

    public ProductInventoryService(
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        IUnitOfWork uow)
    {
        _productRepo = productRepo;
        _inventoryRepo = inventoryRepo;
        _uow = uow;
    }

    // ========== 产品管理 ==========

    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var (items, total) = await _productRepo.GetPagedWithDetailsAsync(pageIndex, pageSize, keyword);

        return ApiResponse<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(MapToDto).ToList()
        });
    }

    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id)
    {
        var product = await _productRepo.GetByIdWithDetailsAsync(id);
        if (product == null)
            return ApiResponse<ProductDto>.Fail("产品不存在", 404);

        return ApiResponse<ProductDto>.Success(MapToDto(product));
    }

    public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Category = dto.Category,
            Unit = dto.Unit,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            SupplierId = dto.SupplierId,
            Status = 1
        };

        try
        {
            await _uow.BeginAsync();

            await _productRepo.AddAsync(product);

            var inventory = new Inventory
            {
                ProductId = product.Id,
                StockQuantity = dto.InitialStock,
                LockedQuantity = 0
            };
            await _inventoryRepo.AddAsync(inventory);

            // 从数据库重新加载关联数据
            product.Inventory = inventory;

            await _uow.CommitAsync();

            return ApiResponse<ProductDto>.Success(MapToDto(product), "产品创建成功");
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        var product = await _productRepo.GetByIdWithDetailsAsync(id);
        if (product == null)
            return ApiResponse<ProductDto>.Fail("产品不存在", 404);

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Unit != null) product.Unit = dto.Unit;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.Status.HasValue) product.Status = dto.Status.Value;

        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();

        return ApiResponse<ProductDto>.Success(MapToDto(product), "产品更新成功");
    }

    public async Task<ApiResponse> DeleteProductAsync(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product == null)
            return ApiResponse.Fail("产品不存在", 404);

        product.Status = 0; // 软删除（下架）
        _productRepo.Update(product);

        return ApiResponse.Success("产品已下架");
    }

    // ========== 库存管理 ==========

    public async Task<ApiResponse<InventoryDto>> GetInventoryAsync(int productId)
    {
        var inv = await _inventoryRepo.GetByProductIdAsync(productId);
        if (inv == null)
            return ApiResponse<InventoryDto>.Fail("库存记录不存在", 404);

        return ApiResponse<InventoryDto>.Success(new InventoryDto
        {
            ProductId = inv.ProductId,
            ProductName = inv.Product?.Name ?? "",
            StockQuantity = inv.StockQuantity,
            LockedQuantity = inv.LockedQuantity,
            AvailableQuantity = inv.AvailableQuantity,
            UpdateTime = inv.UpdateTime
        });
    }

    public async Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10)
    {
        var list = await _inventoryRepo.GetLowStockAsync(threshold);

        var dtos = list.Select(i => new InventoryDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? "",
            StockQuantity = i.StockQuantity,
            LockedQuantity = i.LockedQuantity,
            AvailableQuantity = i.AvailableQuantity,
            UpdateTime = i.UpdateTime
        }).ToList();

        return ApiResponse<List<InventoryDto>>.Success(dtos);
    }

    public async Task<ApiResponse> StockInAsync(UpdateInventoryDto dto)
    {
        var inv = await _inventoryRepo.GetByProductIdAsync(dto.ProductId);
        if (inv == null)
        {
            // 库存记录不存在则创建
            inv = new Inventory
            {
                ProductId = dto.ProductId,
                StockQuantity = dto.Quantity,
                LockedQuantity = 0
            };
            await _inventoryRepo.AddAsync(inv);
        }
        else
        {
            inv.StockQuantity += dto.Quantity;
            inv.UpdateTime = DateTime.Now;
            _inventoryRepo.Update(inv);
        }

        return ApiResponse.Success($"入库成功，当前库存: {inv.StockQuantity}");
    }

    public async Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto)
    {
        var inv = await _inventoryRepo.GetByProductIdAsync(dto.ProductId);
        if (inv == null)
            return ApiResponse.Fail("库存记录不存在", 404);
        if (inv.AvailableQuantity < dto.Quantity)
            return ApiResponse.Fail("库存不足");

        inv.StockQuantity -= dto.Quantity;
        inv.UpdateTime = DateTime.Now;
        _inventoryRepo.Update(inv);

        return ApiResponse.Success($"出库成功，当前库存: {inv.StockQuantity}");
    }

    // ========== 私有方法 ==========

    private static ProductDto MapToDto(Product p)
        => new()
        {
            Id = p.Id,
            Name = p.Name,
            Category = p.Category,
            Unit = p.Unit,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Status = p.Status,
            SupplierName = p.Supplier?.Name ?? "",
            AvailableStock = p.Inventory?.AvailableQuantity ?? 0
        };
}
