using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

// 货物（Inv_Goods）业务接口：供应商维护自己货物 + 管理员查看全部货物。
public interface IGoodsService
{
    // 供应商查看自己的货物
    Task<ApiResponse<List<GoodsDto>>> GetSupplierGoodsAsync(string supplierId);

    // 平台管理员查看全部货物（含归属供应商）
    Task<ApiResponse<List<GoodsDto>>> GetAllGoodsAsync(string? keyword = null);

    // 供应商对现有物品建立自己的货物
    Task<ApiResponse<GoodsDto>> AddGoodsAsync(string supplierId, CreateGoodsDto dto);

    // 供应商维护自己货物（售价/温区/保质期/描述/上下架）
    Task<ApiResponse> UpdateGoodsAsync(string supplierId, string productId, UpdateGoodsDto dto);

    // 供应商上下架自己货物
    Task<ApiResponse> SetGoodsStatusAsync(string supplierId, string productId, string status);

    // 商品管理员对“商品（物品）”整体下架/上架：为保证数据一致，该物品下所有供应商的货物会一并
    // 置为目标状态（下架即全部停售，不存在某个供应商仍在售的残留）。
    Task<ApiResponse<int>> AdminSetProductGoodsStatusAsync(string productId, string status);
}
