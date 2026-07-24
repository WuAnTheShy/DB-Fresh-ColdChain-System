using FreshGroupSystem.Common;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// 产品库存管理服务接口
/// </summary>
public interface IProductInventoryService
{
    // 产品管理
    Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null);
    Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id);
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto);
    Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto);
    Task<ApiResponse> DeleteProductAsync(int id);

    // 库存管理
    Task<ApiResponse<InventoryDto>> GetInventoryAsync(int productId);
    Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10);
    Task<ApiResponse> StockInAsync(UpdateInventoryDto dto);   // 入库
    Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto);  // 出库
}
