using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 冷链发货单仓储接口 — 管理 Log_ExpressDeliveries 表
/// </summary>
public interface ILogExpressDeliveryRepository : IBaseRepository<LogExpressDelivery>
{
    /// <summary>按订单 ID 查询所有发货单（一个订单可能按供应商拆成多张发货单）</summary>
    Task<List<LogExpressDelivery>> GetByOrderIdAsync(string orderId);

    /// <summary>按供应商 ID 查询发货单</summary>
    Task<List<LogExpressDelivery>> GetBySupplierIdAsync(string supplierId);

    /// <summary>按物流单号精确查询</summary>
    Task<LogExpressDelivery?> GetByTrackingNoAsync(string trackingNo);
}
