using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface ISupplierRepository : IBaseRepository<InvSupplier>
{
    Task<List<InvProduct>> GetProductsBySupplierIdAsync(string supplierId);
    Task<(List<InvSupplier> Items, int Total)> GetPagedWithProductCountAsync(int pageIndex, int pageSize);
    Task<InvSupplier?> GetByIdWithProductsAsync(string id);
}
