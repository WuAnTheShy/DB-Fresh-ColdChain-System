using FreshGroupSystem.Common;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// 供应商管理服务接口
/// </summary>
public interface ISupplierService
{
    Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize);
    Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(int id);
    Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto);
    Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(int id, CreateSupplierDto dto);
    Task<ApiResponse> DeleteSupplierAsync(int id);
}
