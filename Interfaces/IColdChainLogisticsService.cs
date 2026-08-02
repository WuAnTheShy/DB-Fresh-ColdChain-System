using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

/// <summary>
/// A组对外提供的冷链运费报价与批次级可追溯发货能力
/// </summary>
public interface IColdChainLogisticsService
{
    /// <summary>根据商品温区、重量、目的地计算阶梯冷链运费</summary>
    Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request);

    /// <summary>按 FEFO 扣减批次库存并发货，记录批次溯源映射</summary>
    Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request);

    // ========== 精准溯源查询 ==========

    /// <summary>按订单 ID 查询该订单的所有发货单及其批次溯源明细（正向溯源）</summary>
    Task<ApiResponse<List<DeliveryTraceDto>>> GetTraceabilityByOrderAsync(string orderId);

    /// <summary>按发货单 ID 查询该发货单的批次扣减明细</summary>
    Task<ApiResponse<DeliveryTraceDto>> GetTraceabilityByDeliveryAsync(string deliveryId);

    /// <summary>反向溯源：按批次 ID 查询该批次被哪些发货单使用</summary>
    Task<ApiResponse<BatchTraceDto>> GetBatchTraceAsync(string batchId);
}
