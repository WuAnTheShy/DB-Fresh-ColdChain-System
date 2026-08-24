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

    // ========== 供货价（进价由供应商决定）==========

    /// <summary>查询某供应商每个产品的供货价（未报价为 null）</summary>
    Task<ApiResponse<List<SupplierProductQuoteDto>>> GetSupplierProductQuotesAsync(string supplierId);

    /// <summary>设置/更新某供应商对某产品的供货价（含供应商声明的保质期，小时）</summary>
    Task<ApiResponse> SetSupplyPriceAsync(string supplierId, string productId, decimal supplyPrice, int? shelfLifeHours = null);

    /// <summary>供应商登录：账号密码校验，成功返回供应商信息</summary>
    Task<ApiResponse<SupplierDto>> SupplierLoginAsync(string loginAccount, string password);

    /// <summary>供应商门户：全部上架产品的报价面板（含自己的当前报价，未报价为 null）</summary>
    Task<ApiResponse<List<SupplierProductQuoteDto>>> GetAllProductQuotesForSupplierAsync(string supplierId);

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
}
