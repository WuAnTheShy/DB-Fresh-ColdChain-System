using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;

namespace FreshGroupSystem.Interfaces;

/// <summary>A组对外提供的冷链报价与可追溯发货能力。</summary>
public interface IColdChainLogisticsService
{
    Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request);
    Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request);
}
