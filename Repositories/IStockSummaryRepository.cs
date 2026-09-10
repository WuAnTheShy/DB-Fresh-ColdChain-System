//操作InvStockSummary表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IStockSummaryRepository : IBaseRepository<InvStockSummary>
{
    Task<InvStockSummary?> GetByProductIdAsync(string productId);//按产品 ID 查询库存汇总信息
    Task<List<InvStockSummary>> GetLowStockAsync(int threshold);//查询库存低于指定阈值的产品列表
    // 带行级锁查询 — 用于防超卖，需在事务内调用
    Task<InvStockSummary?> GetByProductIdForUpdateAsync(string productId);
}
