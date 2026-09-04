using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 货物服务 — 供应商对自己货物（售价/上下架/温区/保质期/描述）的维护，
/// 以及平台管理员查看全部货物。所有供应商侧写操作均校验货物归属（SupplierID == 当前供应商）。
/// </summary>
public class GoodsService : IGoodsService
{
    private readonly IGoodsRepository _goodsRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISupplierRepository _supplierRepo;

    public GoodsService(
        IGoodsRepository goodsRepo,
        IProductRepository productRepo,
        ISupplierRepository supplierRepo)
    {
        _goodsRepo = goodsRepo;
        _productRepo = productRepo;
        _supplierRepo = supplierRepo;
    }

    /// <summary>供应商查看自己的货物（SupplierID = 自己）</summary>
    public async Task<ApiResponse<List<GoodsDto>>> GetSupplierGoodsAsync(string supplierId)
    {
        try
        {
            var goods = await _goodsRepo.GetBySupplierAsync(supplierId);
            return ApiResponse<List<GoodsDto>>.Success(goods.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<List<GoodsDto>>.Fail($"查询货物失败：{ex.Message}");
        }
    }

    /// <summary>平台管理员查看全部货物（含归属供应商）</summary>
    public async Task<ApiResponse<List<GoodsDto>>> GetAllGoodsAsync(string? keyword = null)
    {
        try
        {
            var goods = await _goodsRepo.GetAllAsync(keyword);
            return ApiResponse<List<GoodsDto>>.Success(goods.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<List<GoodsDto>>.Fail($"查询货物失败：{ex.Message}");
        }
    }

    /// <summary>供应商对现有物品建立自己的货物（不设数量，数量后续走进货）</summary>
    public async Task<ApiResponse<GoodsDto>> AddGoodsAsync(string supplierId, CreateGoodsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductID))
            return ApiResponse<GoodsDto>.Fail("请选择物品");
        if (dto.SalePrice < 0)
            return ApiResponse<GoodsDto>.Fail("售价不能为负数");

        try
        {
            var supplier = await _supplierRepo.GetByIdAsync(supplierId);
            if (supplier == null) return ApiResponse<GoodsDto>.Fail("供应商不存在", 404);

            var product = await _productRepo.GetByIdAsync(dto.ProductID);
            if (product == null) return ApiResponse<GoodsDto>.Fail("物品不存在", 404);

            var existing = await _goodsRepo.GetAsync(dto.ProductID, supplierId);
            if (existing != null)
                return ApiResponse<GoodsDto>.Fail("你已对该物品建立过货物，可直接维护");

            var goods = new InvGoods
            {
                ProductID = dto.ProductID,
                SupplierID = supplierId,
                SalePrice = dto.SalePrice,
                Status = "ACTIVE",
                StorageReq = dto.StorageReq ?? product.StorageReq,
                ShelfLifeHours = dto.ShelfLifeHours ?? product.ExpiryHours,
                Description = dto.Description,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
            await _goodsRepo.AddAsync(goods);

            goods.Product = product;
            goods.Supplier = supplier;
            return ApiResponse<GoodsDto>.Success(MapToDto(goods), "货物已建立");
        }
        catch (Exception ex)
        {
            return ApiResponse<GoodsDto>.Fail($"建立货物失败：{ex.Message}");
        }
    }

    /// <summary>供应商维护自己货物：改售价/温区/保质期/描述/上下架</summary>
    public async Task<ApiResponse> UpdateGoodsAsync(string supplierId, string productId, UpdateGoodsDto dto)
    {
        try
        {
            var goods = await _goodsRepo.GetAsync(productId, supplierId);
            if (goods == null)
                return ApiResponse.Fail("未找到该货物，或该货物不属于你", 404);

            if (dto.SalePrice.HasValue)
            {
                if (dto.SalePrice.Value < 0) return ApiResponse.Fail("售价不能为负数");
                goods.SalePrice = dto.SalePrice.Value;
            }
            if (dto.StorageReq != null) goods.StorageReq = dto.StorageReq;
            if (dto.ShelfLifeHours.HasValue) goods.ShelfLifeHours = dto.ShelfLifeHours;
            if (dto.Description != null) goods.Description = dto.Description;
            if (dto.Status != null) goods.Status = dto.Status;

            goods.UpdateTime = DateTime.Now;
            await _goodsRepo.UpdateAsync(goods);
            return ApiResponse.Success("货物已更新");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"更新货物失败：{ex.Message}");
        }
    }

    /// <summary>供应商上下架自己货物</summary>
    public async Task<ApiResponse> SetGoodsStatusAsync(string supplierId, string productId, string status)
    {
        return await UpdateGoodsAsync(supplierId, productId, new UpdateGoodsDto { Status = status });
    }

    private static GoodsDto MapToDto(InvGoods g) => new()
    {
        ProductID = g.ProductID,
        ProductName = g.Product?.ProductName ?? g.ProductID,
        CategoryName = g.Product?.Category?.CategoryName,
        SupplierID = g.SupplierID,
        SupplierName = g.Supplier?.SupplierName ?? g.SupplierID,
        SalePrice = g.SalePrice,
        Status = g.Status,
        StorageReq = g.StorageReq,
        ShelfLifeHours = g.ShelfLifeHours,
        Description = g.Description
    };
}
