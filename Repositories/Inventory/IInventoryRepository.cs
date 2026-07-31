using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 库存仓储接口
/// </summary>
public interface IInventoryRepository : IBaseRepository<Inventory>
{
    Task<Inventory?> GetByProductIdAsync(int productId);
    Task<List<Inventory>> GetLowStockAsync(int threshold);
}
