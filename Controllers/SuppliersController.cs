using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Common;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories.Supplier;

namespace FreshGroupSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _supplierRepo;

    public SuppliersController(ISupplierRepository supplierRepo)
    {
        _supplierRepo = supplierRepo;
    }

    /// <summary>
    /// 分页获取供应商列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var all = await _supplierRepo.GetAllAsync();
        var total = all.Count;
        var items = all
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Address = s.Address,
                Remark = s.Remark,
                ProductCount = s.Products?.Count ?? 0
            })
            .ToList();

        return ApiResponse<PagedResult<SupplierDto>>.Success(new PagedResult<SupplierDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        });
    }

    /// <summary>
    /// 获取供应商详情（含产品列表）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<SupplierDto>> GetDetail(int id)
    {
        var supplier = await _supplierRepo.GetByIdAsync(id);
        if (supplier == null)
            return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);

        var products = await _supplierRepo.GetProductsBySupplierIdAsync(id);

        return ApiResponse<SupplierDto>.Success(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Address = supplier.Address,
            Remark = supplier.Remark,
            ProductCount = products.Count
        });
    }

    /// <summary>
    /// 创建供应商
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<SupplierDto>> Create([FromBody] CreateSupplierDto dto)
    {
        var supplier = new Supplier
        {
            Name = dto.Name,
            ContactPerson = dto.ContactPerson,
            Phone = dto.Phone,
            Address = dto.Address,
            Remark = dto.Remark
        };

        await _supplierRepo.AddAsync(supplier);
        await _supplierRepo.SaveChangesAsync();

        return ApiResponse<SupplierDto>.Success(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Address = supplier.Address,
            Remark = supplier.Remark
        }, "供应商创建成功");
    }

    /// <summary>
    /// 更新供应商
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<SupplierDto>> Update(int id, [FromBody] CreateSupplierDto dto)
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

        return ApiResponse<SupplierDto>.Success(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Address = supplier.Address,
            Remark = supplier.Remark
        }, "供应商更新成功");
    }

    /// <summary>
    /// 删除供应商
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> Delete(int id)
    {
        var supplier = await _supplierRepo.GetByIdAsync(id);
        if (supplier == null)
            return ApiResponse.Fail("供应商不存在", 404);

        _supplierRepo.Delete(supplier);
        await _supplierRepo.SaveChangesAsync();

        return ApiResponse.Success("供应商已删除");
    }
}
