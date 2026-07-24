using Microsoft.AspNetCore.Mvc;
using FreshGroupSystem.Common;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductInventoryService _service;

    public ProductsController(IProductInventoryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页获取产品列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null)
        => await _service.GetProductsAsync(pageIndex, pageSize, keyword);

    /// <summary>
    /// 获取产品详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<ProductDto>> GetDetail(int id)
        => await _service.GetProductByIdAsync(id);

    /// <summary>
    /// 创建产品
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<ProductDto>> Create([FromBody] CreateProductDto dto)
        => await _service.CreateProductAsync(dto);

    /// <summary>
    /// 更新产品
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse<ProductDto>> Update(int id, [FromBody] UpdateProductDto dto)
        => await _service.UpdateProductAsync(id, dto);

    /// <summary>
    /// 下架产品
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> Delete(int id)
        => await _service.DeleteProductAsync(id);

    // ========== 库存接口 ==========

    /// <summary>
    /// 获取产品库存
    /// </summary>
    [HttpGet("{productId}/inventory")]
    public async Task<ApiResponse<InventoryDto>> GetInventory(int productId)
        => await _service.GetInventoryAsync(productId);

    /// <summary>
    /// 获取低库存产品
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ApiResponse<List<InventoryDto>>> GetLowStock([FromQuery] int threshold = 10)
        => await _service.GetLowStockProductsAsync(threshold);

    /// <summary>
    /// 入库
    /// </summary>
    [HttpPost("stock-in")]
    public async Task<ApiResponse> StockIn([FromBody] UpdateInventoryDto dto)
        => await _service.StockInAsync(dto);

    /// <summary>
    /// 出库
    /// </summary>
    [HttpPost("stock-out")]
    public async Task<ApiResponse> StockOut([FromBody] UpdateInventoryDto dto)
        => await _service.StockOutAsync(dto);
}
