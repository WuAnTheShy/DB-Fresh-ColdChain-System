//供应商服务接口

using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

public interface ISupplierService
{
    Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize);
    Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(string id);
    Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto);
    Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(string id, CreateSupplierDto dto);
    Task<ApiResponse> DeleteSupplierAsync(string id);

    // ========== 供货价（进价由供应商决定，统一以「我的货物」售价为准）==========

    /// <summary>查询某供应商已建立货物的商品列表（含售价/保质期，供应商详情页用）</summary>
    Task<ApiResponse<List<SupplierProductQuoteDto>>> GetSupplierProductQuotesAsync(string supplierId);


    /// <summary>
    /// 供应商为自己供货的商品新增一张图片（二进制写入 Inv_ProductImages.ImageData BLOB，
    /// 展示顺序接在现有图片之后，对外地址为 /images/product/{ImageID}）。
    /// 该供应商须已对该物品建立货物，且每商品最多 9 张图片。
    /// </summary>
    Task<ApiResponse> AddProductImageAsync(string supplierId, string productId, byte[] imageData, string imageType);

    /// <summary>
    /// 按图片 ID 查询图片二进制与 MIME 类型（供 /images/product/{id} 接口返回）。
    /// </summary>
    Task<ApiResponse<ProductImageContentDto>> GetProductImageContentAsync(string imageId);

    /// <summary>
    /// 供应商删除自己供货商品的某张图片。
    /// 仅允许删除自己供货（已建立货物）商品的图片。
    /// </summary>
    Task<ApiResponse<string>> DeleteProductImageAsync(string supplierId, string imageId);

    // ========== 跨组接口（供 C 组调用）==========

    /// <summary>
    /// 按条件查询供应商账户信息（不含密码）
    /// C 组可通过 ID/名称/登录账号/电话 任一条件查询
    /// </summary>
    Task<ApiResponse<List<SupplierAccountDto>>> FindSupplierAccountAsync(
        string? supplierId = null,
        string? supplierName = null,
        string? loginAccount = null,
        string? contactPhone = null);

    /// <summary>
    /// 验证供应商登录密码
    /// </summary>
    Task<ApiResponse<bool>> VerifySupplierPasswordAsync(string loginAccount, string password);

    /// <summary>
    /// 商品上架搜索（C 组团长“商品上架”模块用）：
    /// 关键词可以是供应商名称/供应商ID —— 返回该供应商提供的全部商品；
    /// 也可以是商品名称 —— 返回所有供货该商品的（供应商×商品）组合。
    /// 两种命中合并去重后返回，每个条目代表一个可入团的（供应商，商品）。
    /// 可提供商品以供应商的货物记录（Inv_Goods）为准。
    /// </summary>
    Task<ApiResponse<List<SupplierProductEntryDto>>> SearchSupplierProductEntriesAsync(string? keyword);

    // ========== 管理端（管理员角色管理用）==========

    /// <summary>全部供应商列表（含状态），供管理员启禁用管理</summary>
    Task<ApiResponse<List<SupplierDto>>> GetAllSuppliersAsync();

    /// <summary>按状态查询供应商（如 Pending 待审核列表）</summary>
    Task<ApiResponse<List<SupplierDto>>> GetSuppliersByStatusAsync(string status);

    /// <summary>变更供应商状态（Active/Pending/Disabled/Rejected），含状态流转校验</summary>
    Task<ApiResponse> SetSupplierStatusAsync(string supplierId, string targetStatus);
}
