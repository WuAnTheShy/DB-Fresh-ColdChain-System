// 产品与库存服务接口

using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

public interface IProductInventoryService
{
    // 产品
    Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null);
    Task<ApiResponse<ProductDto>> GetProductByIdAsync(string id);

    /// <summary>
    /// 产品详情页"供应商图文"：报价该商品的供应商列表；若指定 supplierId，
    /// 返回该供应商的简介与图片（自己上传的在前，平台通用图在后）。
    /// </summary>
    Task<ApiResponse<ProductSupplierMediaDto>> GetSupplierProductMediaAsync(string productId, string? supplierId);
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto);
    Task<ApiResponse<ProductDto>> UpdateProductAsync(string id, UpdateProductDto dto);
    Task<ApiResponse> DeleteProductAsync(string id);

    /// <summary>
    /// 为商品批量追加图片（平台通用图，SupplierID 置空；BLOB 写入 Inv_ProductImages）。
    /// 图片对外地址统一为 /images/product/{ImageID}，新增商品/编辑商品时随表单提交后调用。
    /// </summary>
    Task<ApiResponse> AddProductImagesAsync(string productId, IReadOnlyList<ProductImageUploadDto> images);

    // 分类
    Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync();
    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto);

    // 跨组接口（供 C 组调用）

    /// <summary>
    /// 查询商品库存总量
    /// </summary>
    Task<ApiResponse<int>> GetProductStockAsync(string productId);

    /// <summary>
    /// 查询某 (商品, 供应商) 的货物信息与供应商级可用量。
    /// 同一商品可由多个供应商供货，各自拥有独立售价与库存批次。
    /// </summary>
    Task<ApiResponse<SupplierGoodsInventoryDto>> GetSupplierGoodsInventoryAsync(string productId, string supplierId);

    // 库存
    Task<ApiResponse<InventoryDto>> GetInventoryAsync(string productId);
    Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10);
    Task<ApiResponse> StockInAsync(UpdateInventoryDto dto, string? supplierId = null, string? batchNo = null, DateTime? productionDate = null);
    /// <summary>入库可选供应商下拉：该产品所有已报价的供应商</summary>
    Task<ApiResponse<List<SupplierQuoteOptionDto>>> GetStockInSupplierOptionsAsync(string productId);
    Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto);

    // 批次
    Task<ApiResponse<List<StockBatchDto>>> GetBatchesAsync(string productId);
    Task<ApiResponse<StockBatchDto>> AddBatchAsync(CreateStockBatchDto dto);
    /// <summary>自动标记已过期批次为EXPIRED</summary>
    Task MarkExpiredBatchesAsync();
}
