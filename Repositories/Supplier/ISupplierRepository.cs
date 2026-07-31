using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Supplier;

/// <summary>
/// 供应商仓储接口
/// </summary>
public interface ISupplierRepository : IBaseRepository<Models.Supplier>
{
    Task<List<Product>> GetProductsBySupplierIdAsync(int supplierId);
    Task<(List<Models.Supplier> Items, int Total)> GetPagedWithProductCountAsync(int pageIndex, int pageSize);
    Task<Models.Supplier?> GetByIdWithProductsAsync(int id);
}
