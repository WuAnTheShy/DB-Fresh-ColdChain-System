using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

/// <summary>货物（Inv_Goods）业务接口：供应商维护自己货物 + 管理员查看全部货物。</summary>
public interface IGoodsService
{
    /// <summary>供应商查看自己的货物</summary>
    Task<ApiResponse<List<GoodsDto>>> GetSupplierGoodsAsync(string supplierId);

    /// <summary>平台管理员查看全部货物（含归属供应商）</summary>
    Task<ApiResponse<List<GoodsDto>>> GetAllGoodsAsync(string? keyword = null);

    /// <summary>供应商对现有物品建立自己的货物</summary>
    Task<ApiResponse<GoodsDto>> AddGoodsAsync(string supplierId, CreateGoodsDto dto);

    /// <summary>供应商维护自己货物（售价/温区/保质期/描述/上下架）</summary>
    Task<ApiResponse> UpdateGoodsAsync(string supplierId, string productId, UpdateGoodsDto dto);

    /// <summary>供应商上下架自己货物</summary>
    Task<ApiResponse> SetGoodsStatusAsync(string supplierId, string productId, string status);
}
