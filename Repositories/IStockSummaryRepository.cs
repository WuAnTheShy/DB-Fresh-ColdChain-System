using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IStockSummaryRepository : IBaseRepository<InvStockSummary>
{
    Task<InvStockSummary?> GetByProductIdAsync(string productId);
    Task<List<InvStockSummary>> GetLowStockAsync(int threshold);
}
