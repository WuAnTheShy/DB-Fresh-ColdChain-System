using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Interfaces;

/// <summary>
/// A组对外提供的冷链运费报价与批次级可追溯发货能力
/// </summary>
public interface IColdChainLogisticsService
{
    /// <summary>
    /// 根据商品温区、重量、目的地计算阶梯冷链运费
    /// </summary>
    Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request);

    /// <summary>
    /// 按 FEFO 扣减批次库存并发货，记录批次溯源映射
    /// </summary>
    Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request);
}
