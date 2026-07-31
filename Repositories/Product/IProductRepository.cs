using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 产品仓储接口
/// </summary>
public interface IProductRepository : IBaseRepository<Product>
{
    Task<(List<Product> Items, int Total)> GetPagedWithDetailsAsync(int pageIndex, int pageSize, string? keyword = null);
    Task<Product?> GetByIdWithDetailsAsync(int id);
}
