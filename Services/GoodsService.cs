using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// 货物服务 — 供应商对自己货物（售价/上下架/温区/保质期/描述）的维护，
// 以及平台管理员查看全部货物。所有供应商侧写操作均校验货物归属（SupplierID == 当前供应商）。
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

    // 供应商查看自己的货物（SupplierID = 自己）
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

    // 平台管理员查看全部货物（含归属供应商）
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

    // 供应商对现有物品建立自己的货物（不设数量，数量后续走进货）
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
                ShelfLifeHours = dto.ShelfLifeHours ?? product.ExpiryHours,
                Description = dto.Description,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
            await _goodsRepo.AddAsync(goods);

            // 温区跟随物品，不落库；这里只是把物品的温区带出来供返回展示
            goods.StorageReq = product.StorageReq;
            goods.Product = product;
            goods.Supplier = supplier;
            return ApiResponse<GoodsDto>.Success(MapToDto(goods), "货物已建立");
        }
        catch (Exception ex)
        {
            return ApiResponse<GoodsDto>.Fail($"建立货物失败：{ex.Message}");
        }
    }

    // 供应商维护自己货物：改售价/温区/保质期/描述/上下架
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
            if (dto.ShelfLifeHours.HasValue)
            {
                if (dto.ShelfLifeHours.Value <= 0) return ApiResponse.Fail("保质期必须大于 0 小时");
                goods.ShelfLifeHours = dto.ShelfLifeHours.Value;
            }
            if (dto.Description != null)
                goods.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
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

    // 供应商上下架自己货物
    public async Task<ApiResponse> SetGoodsStatusAsync(string supplierId, string productId, string status)
    {
        return await UpdateGoodsAsync(supplierId, productId, new UpdateGoodsDto { Status = status });
    }

    // 商品管理员对“商品（物品）”整体下架/上架：连带该物品所有供应商的货物统一置为目标状态，
    // 保证商品下架时不会残留个别供应商仍在上架的货物，数据始终一致。
    public async Task<ApiResponse<int>> AdminSetProductGoodsStatusAsync(string productId, string status)
    {
        var st = status?.Trim().ToUpperInvariant();
        if (st != "ACTIVE" && st != "INACTIVE")
            return ApiResponse<int>.Fail("无效的状态值（仅支持 ACTIVE / INACTIVE）");

        try
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null) return ApiResponse<int>.Fail("物品不存在", 404);

            var affected = await _goodsRepo.UpdateStatusByProductAsync(productId, st, DateTime.Now);
            var action = st == "ACTIVE" ? "上架" : "下架";
            if (affected == 0)
                return ApiResponse<int>.Fail($"该物品暂无供应商建立货物，无需{action}");

            return ApiResponse<int>.Success(affected, $"已{action}该物品下全部 {affected} 个供应商的货物");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"按商品{(st == "ACTIVE" ? "上架" : "下架")}失败：{ex.Message}");
        }
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
