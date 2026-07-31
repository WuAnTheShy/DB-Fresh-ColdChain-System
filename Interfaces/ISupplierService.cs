using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

public interface ISupplierService
{
    Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize);
    Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(string id);
    Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto);
    Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(string id, CreateSupplierDto dto);
    Task<ApiResponse> DeleteSupplierAsync(string id);
}
