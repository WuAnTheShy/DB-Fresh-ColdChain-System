//操作InvStockSummary表

using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IStockSummaryRepository : IBaseRepository<InvStockSummary>
{
    Task<InvStockSummary?> GetByProductIdAsync(string productId);//按产品 ID 查询库存汇总信息
    Task<List<InvStockSummary>> GetLowStockAsync(int threshold);//查询库存低于指定阈值的产品列表
}
