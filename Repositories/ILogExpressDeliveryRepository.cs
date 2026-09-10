using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Repositories;

// 冷链发货单仓储接口 — 管理 Log_ExpressDeliveries 表
public interface ILogExpressDeliveryRepository : IBaseRepository<LogExpressDelivery>
{
    // 按订单 ID 查询所有发货单（一个订单可能按供应商拆成多张发货单）
    Task<List<LogExpressDelivery>> GetByOrderIdAsync(string orderId);

    // 按供应商 ID 查询发货单
    Task<List<LogExpressDelivery>> GetBySupplierIdAsync(string supplierId);

    // 按物流单号精确查询
    Task<LogExpressDelivery?> GetByTrackingNoAsync(string trackingNo);

    // 有发货记录的订单（DISTINCT OrderID + Biz_Orders.OrderNo，供溯源下拉）
    Task<List<ShippedOrderOptionDto>> GetDistinctOrdersAsync();
}
