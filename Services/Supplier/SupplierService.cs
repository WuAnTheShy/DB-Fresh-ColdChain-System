using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories.Supplier;

namespace FreshGroupSystem.Services.Supplier;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepo;

    public SupplierService(ISupplierRepository supplierRepo)
    {
        _supplierRepo = supplierRepo;
    }

    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize)
    {
        var (items, total) = await _supplierRepo.GetPagedWithProductCountAsync(pageIndex, pageSize);

        var dtoItems = items.Select(s => new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Address = s.Address,
            Remark = s.Remark,
            ProductCount = s.ProductCount
        }).ToList();

        return ApiResponse<PagedResult<SupplierDto>>.Success(new PagedResult<SupplierDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = dtoItems
        });
    }

    public async Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(int id)
    {
        var supplier = await _supplierRepo.GetByIdWithProductsAsync(id);
        if (supplier == null)
            return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);

        return ApiResponse<SupplierDto>.Success(MapToDto(supplier));
    }

    public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto)
    {
        var supplier = new Models.Supplier
        {
            Name = dto.Name,
            ContactPerson = dto.ContactPerson,
            Phone = dto.Phone,
            Address = dto.Address,
            Remark = dto.Remark
        };

        await _supplierRepo.AddAsync(supplier);
        await _supplierRepo.SaveChangesAsync();

        return ApiResponse<SupplierDto>.Success(MapToDto(supplier), "供应商创建成功");
    }

    public async Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(int id, CreateSupplierDto dto)
    {
        var supplier = await _supplierRepo.GetByIdAsync(id);
        if (supplier == null)
            return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);

        supplier.Name = dto.Name;
        supplier.ContactPerson = dto.ContactPerson;
        supplier.Phone = dto.Phone;
        supplier.Address = dto.Address;
        supplier.Remark = dto.Remark;

        _supplierRepo.Update(supplier);
        await _supplierRepo.SaveChangesAsync();

        return ApiResponse<SupplierDto>.Success(MapToDto(supplier), "供应商更新成功");
    }

    public async Task<ApiResponse> DeleteSupplierAsync(int id)
    {
        var supplier = await _supplierRepo.GetByIdAsync(id);
        if (supplier == null)
            return ApiResponse.Fail("供应商不存在", 404);

        _supplierRepo.Delete(supplier);
        await _supplierRepo.SaveChangesAsync();

        return ApiResponse.Success("供应商已删除");
    }

    private static SupplierDto MapToDto(Models.Supplier s)
        => new()
        {
            Id = s.Id,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Address = s.Address,
            Remark = s.Remark,
            ProductCount = s.Products?.Count ?? s.ProductCount
        };
}
