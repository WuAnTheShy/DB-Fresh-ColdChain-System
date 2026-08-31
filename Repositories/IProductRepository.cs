//操作InvProduct表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IProductRepository : IBaseRepository<InvProduct>
{
    Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);//分页查询产品列表，包含分类、供应商、价格规则和库存信息
    Task<InvProduct?> GetByIdWithDetailsAsync(string id);//按产品 ID 查询产品详情，包含分类、供应商、价格规则和库存信息

    /// <summary>查询全部商品图片（服务层内存分组，按商品取前 3 张）</summary>
    Task<List<InvProductImage>> GetAllProductImagesAsync();
}
