//操作InvProduct表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IProductRepository : IBaseRepository<InvProduct>
{
    Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);//分页查询产品列表，包含分类、供应商、价格规则和库存信息
    Task<InvProduct?> GetByIdWithDetailsAsync(string id);//按产品 ID 查询产品详情，包含分类、供应商、价格规则和库存信息

    /// <summary>查询全部商品图片（服务层内存分组，按商品取前 3 张）</summary>
    Task<List<InvProductImage>> GetAllProductImagesAsync();

    /// <summary>查询全部商品的上下架状态（INV_PRODUCTS.STATUS），返回 ProductID → STATUS</summary>
    Task<Dictionary<string, string?>> GetProductStatusMapAsync();

    /// <summary>查询某商品的全部图片（按展示顺序升序）</summary>
    Task<List<InvProductImage>> GetProductImagesAsync(string productId);

    /// <summary>按图片 ID 查询单张图片</summary>
    Task<InvProductImage?> GetProductImageByIdAsync(string imageId);

    /// <summary>新增商品图片（供应商上传，含 BLOB 二进制数据）</summary>
    Task AddProductImageAsync(InvProductImage image);

    /// <summary>按图片 ID 查询图片二进制与 MIME 类型（供 /images/product/{id} 接口流式返回）</summary>
    Task<(byte[]? Data, string? ContentType)?> GetProductImageDataAsync(string imageId);

    /// <summary>按图片 ID 删除商品图片</summary>
    Task DeleteProductImageAsync(string imageId);
}
