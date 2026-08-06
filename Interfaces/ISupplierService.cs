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
