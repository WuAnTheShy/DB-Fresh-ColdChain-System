using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 批次溯源映射仓储实现
/// 支持双向追踪：正向（发货单→用了哪些批次）、反向（批次→被哪些发货单使用）
/// </summary>
public class LogFulfillmentBatchItemRepository : BaseRepository<LogFulfillmentBatchItem>, ILogFulfillmentBatchItemRepository
{
    public LogFulfillmentBatchItemRepository(IUnitOfWork uow) : base(uow) { }

    /// <summary>正向溯源：按发货单 ID 查所有批次扣减明细</summary>
    public async Task<List<LogFulfillmentBatchItem>> GetByDeliveryIdAsync(string deliveryId)
    {
        var sql = """SELECT * FROM "Log_FulfillmentBatchItems" WHERE "DeliveryID" = :Id ORDER BY "AllocationID" """;
        return (await _uow.Connection.QueryAsync<LogFulfillmentBatchItem>(sql, new { Id = deliveryId }, _uow.Transaction)).ToList();
    }

    /// <summary>反向溯源：按批次 ID 查该批次的所有发货记录</summary>
    public async Task<List<LogFulfillmentBatchItem>> GetByBatchIdAsync(string batchId)
    {
        var sql = """SELECT * FROM "Log_FulfillmentBatchItems" WHERE "BatchID" = :Id ORDER BY "AllocationID" """;
        return (await _uow.Connection.QueryAsync<LogFulfillmentBatchItem>(sql, new { Id = batchId }, _uow.Transaction)).ToList();
    }

    /// <summary>按产品 ID 查所有批次扣减记录</summary>
    public async Task<List<LogFulfillmentBatchItem>> GetByProductIdAsync(string productId)
    {
        var sql = """SELECT * FROM "Log_FulfillmentBatchItems" WHERE "ProductID" = :Id ORDER BY "AllocationID" """;
        return (await _uow.Connection.QueryAsync<LogFulfillmentBatchItem>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }
}
