using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 批次溯源映射仓储接口 — 管理 Log_FulfillmentBatchItems 表
// 核心能力：正向溯源（发货单→批次）和反向溯源（批次→发货单）
public interface ILogFulfillmentBatchItemRepository : IBaseRepository<LogFulfillmentBatchItem>
{
    // 正向溯源：按发货单 ID 查所有批次扣减明细（含商品名、批次号）
    Task<List<LogFulfillmentBatchItem>> GetByDeliveryIdAsync(string deliveryId);

    // 反向溯源：按批次 ID 查该批次被哪些发货单使用
    Task<List<LogFulfillmentBatchItem>> GetByBatchIdAsync(string batchId);

    // 按产品 ID 查所有批次扣减记录
    Task<List<LogFulfillmentBatchItem>> GetByProductIdAsync(string productId);
}
