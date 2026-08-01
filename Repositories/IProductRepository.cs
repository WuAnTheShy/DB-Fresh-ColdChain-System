//操作InvProduct表

using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IProductRepository : IBaseRepository<InvProduct>
{
    Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);//分页查询产品列表，包含分类、供应商、价格规则和库存信息
    Task<InvProduct?> GetByIdWithDetailsAsync(string id);//按产品 ID 查询产品详情，包含分类、供应商、价格规则和库存信息
}
