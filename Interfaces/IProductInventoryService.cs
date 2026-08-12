//产品与库存服务接口

using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

public interface IProductInventoryService
{
    // 产品
    Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null);
    Task<ApiResponse<ProductDto>> GetProductByIdAsync(string id);
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto);
    Task<ApiResponse<ProductDto>> UpdateProductAsync(string id, UpdateProductDto dto);
    Task<ApiResponse> DeleteProductAsync(string id);

    // 分类
    Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync();
    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto);

    // ========== 跨组接口（供 C 组调用）==========

    /// <summary>
    /// 查询商品库存总量
    /// </summary>
    Task<ApiResponse<int>> GetProductStockAsync(string productId);

    // 库存
    Task<ApiResponse<InventoryDto>> GetInventoryAsync(string productId);
    Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10);
    Task<ApiResponse> StockInAsync(UpdateInventoryDto dto, string? batchNo = null, DateTime? productionDate = null, DateTime? expiryDate = null);
    Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto);

    // 批次
    Task<ApiResponse<List<StockBatchDto>>> GetBatchesAsync(string productId);
    Task<ApiResponse<StockBatchDto>> AddBatchAsync(CreateStockBatchDto dto);
}
