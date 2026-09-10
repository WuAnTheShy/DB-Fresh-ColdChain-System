using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

// A组对外提供的冷链运费报价与批次级可追溯发货能力
public interface IColdChainLogisticsService
{
    // 根据商品温区、重量、目的地计算阶梯冷链运费
    Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request);

    // 按 FEFO 扣减批次库存并发货，记录批次溯源映射
    Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request);

    // 精准溯源查询

    // 按订单 ID 查询该订单的所有发货单及其批次溯源明细（正向溯源）
    Task<ApiResponse<List<DeliveryTraceDto>>> GetTraceabilityByOrderAsync(string orderId);

    // 按发货单 ID 查询该发货单的批次扣减明细
    Task<ApiResponse<DeliveryTraceDto>> GetTraceabilityByDeliveryAsync(string deliveryId);

    // 反向溯源：按批次 ID 查询该批次被哪些发货单使用
    Task<ApiResponse<BatchTraceDto>> GetBatchTraceAsync(string batchId);
}
