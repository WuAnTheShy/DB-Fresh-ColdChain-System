//操作InvProduct表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IProductRepository : IBaseRepository<InvProduct>
{
    Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);//分页查询产品列表，包含分类、供应商、价格规则和库存信息
    Task<InvProduct?> GetByIdWithDetailsAsync(string id);//按产品 ID 查询产品详情，包含分类、供应商、价格规则和库存信息
    Task<(List<InvProduct> Items, int Total)> GetPagedBySuppliersAsync(IReadOnlyList<string> supplierIds, string? keyword, string? categoryId, int pageIndex, int pageSize);//按供应商集合分页查询上架商品（含库存汇总），供 B 组可售商品查询
    Task<List<InvProduct>> GetByIdsWithDetailsAsync(IReadOnlyList<string> ids);//按商品编号集合批量查询（含库存汇总），供 B 组可信信息查询
}
