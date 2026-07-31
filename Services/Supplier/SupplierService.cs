using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;

namespace FreshGroupSystem.Services.Supplier;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repo;

    public SupplierService(ISupplierRepository repo) => _repo = repo;

    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize)
    {
        var (items, total) = await _repo.GetPagedWithProductCountAsync(pageIndex, pageSize);
        return ApiResponse<PagedResult<SupplierDto>>.Success(new PagedResult<SupplierDto>
        {
            PageIndex = pageIndex, PageSize = pageSize, TotalCount = total,
            Items = items.Select(MapToDto).ToList()
        });
    }

    public async Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(string id)
    {
        var s = await _repo.GetByIdWithProductsAsync(id);
        if (s == null) return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);
        return ApiResponse<SupplierDto>.Success(MapToDto(s));
    }

    public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto)
    {
        var s = new InvSupplier
        {
            SupplierName = dto.SupplierName, LicenseNo = dto.LicenseNo,
            ExpiryDate = dto.ExpiryDate, CreditLevel = dto.CreditLevel,
            ContactPhone = dto.ContactPhone, LoginAccount = dto.LoginAccount,
            LoginPassword = dto.LoginPassword
        };
        await _repo.AddAsync(s);
        return ApiResponse<SupplierDto>.Success(MapToDto(s), "供应商创建成功");
    }

    public async Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(string id, CreateSupplierDto dto)
    {
        var s = await _repo.GetByIdAsync(id);
        if (s == null) return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);
        s.SupplierName = dto.SupplierName; s.LicenseNo = dto.LicenseNo;
        s.ExpiryDate = dto.ExpiryDate; s.CreditLevel = dto.CreditLevel;
        s.ContactPhone = dto.ContactPhone; s.LoginAccount = dto.LoginAccount;
        if (dto.LoginPassword != null) s.LoginPassword = dto.LoginPassword;
        _repo.Update(s);
        return ApiResponse<SupplierDto>.Success(MapToDto(s), "供应商更新成功");
    }

    public async Task<ApiResponse> DeleteSupplierAsync(string id)
    {
        var s = await _repo.GetByIdAsync(id);
        if (s == null) return ApiResponse.Fail("供应商不存在", 404);
        _repo.Delete(s);
        return ApiResponse.Success("供应商已删除");
    }

    private static SupplierDto MapToDto(InvSupplier s) => new()
    {
        SupplierID = s.SupplierID, SupplierName = s.SupplierName,
        LicenseNo = s.LicenseNo, ExpiryDate = s.ExpiryDate,
        CreditLevel = s.CreditLevel, ContactPhone = s.ContactPhone,
        LoginAccount = s.LoginAccount,
        ProductCount = s.Products?.Count ?? s.ProductCount
    };
}
