// 操作InvSupplier表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ISupplierRepository : IBaseRepository<InvSupplier>
{
    Task<List<InvProduct>> GetProductsBySupplierIdAsync(string supplierId);//按供应商 ID 查询其供应的产品列表
    Task<(List<InvSupplier> Items, int Total)> GetPagedWithProductCountAsync(int pageIndex, int pageSize);//分页查询供应商列表，包含其已报价的产品数量
    Task<InvSupplier?> GetByIdWithProductsAsync(string id);//按供应商 ID 查询供应商详情，包含其供应的产品列表
}
