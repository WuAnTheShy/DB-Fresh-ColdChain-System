using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Common;
using FreshGroupSystem.Data;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Services;

public class ProductInventoryService : IProductInventoryService
{
    private readonly AppDbContext _context;

    public ProductInventoryService(AppDbContext context)
    {
        _context = context;
    }

    // ========== 产品管理 ==========
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null)
    {
        var query = _context.Products
            .Include(p => p.Supplier)
            .Include(p => p.Inventory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.Contains(keyword));

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return ApiResponse<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id)
    {
        var product = await _context.Products
            .Include(p => p.Supplier)
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == id);

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
            Status = 1,
            Inventory = new Inventory
            {
                StockQuantity = dto.InitialStock,
                LockedQuantity = 0
            }
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // 重新加载关联数据
        await _context.Entry(product).Reference(p => p.Supplier).LoadAsync();
        await _context.Entry(product).Reference(p => p.Inventory).LoadAsync();

        return ApiResponse<ProductDto>.Success(MapToDto(product), "产品创建成功");
    }

    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        var product = await _context.Products
            .Include(p => p.Supplier)
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return ApiResponse<ProductDto>.Fail("产品不存在", 404);

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Unit != null) product.Unit = dto.Unit;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.Status.HasValue) product.Status = dto.Status.Value;

        await _context.SaveChangesAsync();

        return ApiResponse<ProductDto>.Success(MapToDto(product), "产品更新成功");
    }

    public async Task<ApiResponse> DeleteProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return ApiResponse.Fail("产品不存在", 404);

        product.Status = 0; // 软删除（下架），不真删
        await _context.SaveChangesAsync();

        return ApiResponse.Success("产品已下架");
    }

    // ========== 库存管理 ==========
    public async Task<ApiResponse<InventoryDto>> GetInventoryAsync(int productId)
    {
        var inv = await _context.Inventories
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == productId);

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
        var list = await _context.Inventories
            .Include(i => i.Product)
            .Where(i => i.StockQuantity <= threshold)
            .Select(i => new InventoryDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product!.Name,
                StockQuantity = i.StockQuantity,
                LockedQuantity = i.LockedQuantity,
                AvailableQuantity = i.AvailableQuantity,
                UpdateTime = i.UpdateTime
            })
            .ToListAsync();

        return ApiResponse<List<InventoryDto>>.Success(list);
    }

    public async Task<ApiResponse> StockInAsync(UpdateInventoryDto dto)
    {
        var inv = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == dto.ProductId);

        if (inv == null)
            return ApiResponse.Fail("库存记录不存在", 404);

        inv.StockQuantity += dto.Quantity;
        inv.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResponse.Success($"入库成功，当前库存: {inv.StockQuantity}");
    }

    public async Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto)
    {
        var inv = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == dto.ProductId);

        if (inv == null)
            return ApiResponse.Fail("库存记录不存在", 404);
        if (inv.AvailableQuantity < dto.Quantity)
            return ApiResponse.Fail("库存不足");

        inv.StockQuantity -= dto.Quantity;
        inv.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

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
