using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IProductRepository : IBaseRepository<InvProduct>
{
    Task<(List<InvProduct> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);
    Task<InvProduct?> GetByIdWithDetailsAsync(string id);
}
