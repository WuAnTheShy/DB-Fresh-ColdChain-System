using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 冷链物流服务：按温层重量阶梯计费，FEFO 批次扣减发货，记录批次溯源
/// </summary>
public class ColdChainLogisticsService : IColdChainLogisticsService
{
    private readonly IProductRepository _products;
    private readonly IStockBatchRepository _batches;
    private readonly IBaseRepository<LogFreightTemplate> _templates;
    private readonly IBaseRepository<LogExpressDelivery> _deliveries;
    private readonly IBaseRepository<LogFulfillmentBatchItem> _allocations;
    private readonly IUnitOfWork _uow;

    public ColdChainLogisticsService(
        IProductRepository products,
        IStockBatchRepository batches,
        IBaseRepository<LogFreightTemplate> templates,
        IBaseRepository<LogExpressDelivery> deliveries,
        IBaseRepository<LogFulfillmentBatchItem> allocations,
        IUnitOfWork uow)
    {
        _products = products;
        _batches = batches;
        _templates = templates;
        _deliveries = deliveries;
        _allocations = allocations;
        _uow = uow;
    }

    /// <summary>
    /// 阶梯冷链运费计算：按地区 + 温层匹配规则，首重/续重 + 冷链包装费
    /// </summary>
    public async Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request)
    {
        if (request.Items.Count == 0)
            return ApiResponse<FreightQuoteDto>.Fail("至少需要一个商品");

        var rules = await _templates.GetAllAsync();
        decimal total = 0;

        foreach (var item in request.Items)
        {
            // 1. 校验商品
            var product = await _products.GetByIdAsync(item.ProductID);
            if (product == null || item.Quantity <= 0 || product.WeightKG is not > 0)
                return ApiResponse<FreightQuoteDto>.Fail("商品、数量或计费重量无效");

            // 2. 确定温区
            var zone = string.IsNullOrWhiteSpace(product.StorageReq)
                ? "CHILLED"
                : product.StorageReq.ToUpperInvariant();

            // 3. 按地区优先级匹配运费规则（省 > 市 > 区 > 通配 *）
            var matchedRule = rules
                .Where(r => r.IsEnabled == 1
                    && r.TemperatureZone == zone
                    && (r.DestinationProvince == "*" || r.DestinationProvince == request.Province)
                    && (r.DestinationCity == "*" || r.DestinationCity == request.City)
                    && (r.DestinationDistrict == "*" || r.DestinationDistrict == request.District))
                .OrderByDescending(r =>
                    (r.DestinationProvince != "*" ? 4 : 0)
                    + (r.DestinationCity != "*" ? 2 : 0)
                    + (r.DestinationDistrict != "*" ? 1 : 0))
                .FirstOrDefault();

            if (matchedRule == null)
                return ApiResponse<FreightQuoteDto>.Fail($"缺少 {zone} 温层运费规则");

            // 4. 包邮判断
            if (matchedRule.FreeShippingThreshold.HasValue
                && request.GoodsAmount >= matchedRule.FreeShippingThreshold)
                continue;

            // 5. 按重量阶梯计费：首重 + 续重 × 续重单价 + 冷链包装费
            var weight = product.WeightKG.Value * item.Quantity;
            var extraUnits = Math.Max(0,
                decimal.Ceiling((weight - matchedRule.BaseWeight) / matchedRule.ExtraWeightUnit));
            total += matchedRule.BaseFee
                + extraUnits * matchedRule.ExtraWeightFee
                + matchedRule.PackagingFee;
        }

        return ApiResponse<FreightQuoteDto>.Success(new FreightQuoteDto
        {
            FreightAmount = decimal.Round(total, 2),
            RuleSummary = "按地区、温层、首重和续重计算"
        });
    }

    /// <summary>
    /// FEFO 批次扣减发货：锁定最早过期批次 → 扣减 → 记录批次溯源
    /// </summary>
    public async Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderID)
            || string.IsNullOrWhiteSpace(request.SupplierID)
            || request.Items.Count == 0)
            return ApiResponse<LogExpressDelivery>.Fail("发货参数不完整");

        try
        {
            await _uow.BeginAsync();

            // 创建发货单
            var delivery = new LogExpressDelivery
            {
                OrderID = request.OrderID,
                SupplierID = request.SupplierID,
                TrackingNo = $"CC{Guid.NewGuid():N}"[..20]
            };
            await _deliveries.AddAsync(delivery);

            // 逐商品按 FEFO 扣减批次
            foreach (var item in request.Items)
            {
                var remaining = item.Quantity;
                var batches = await _batches.GetByProductIdAsync(item.ProductID);

                foreach (var batch in batches.OrderBy(b => b.ExpiryDate))
                {
                    if (remaining <= 0) break;

                    var deduct = Math.Min(remaining, batch.CurrentQty);
                    if (deduct <= 0) continue;

                    batch.CurrentQty -= deduct;
                    if (batch.CurrentQty == 0)
                        batch.Status = "DEPLETED";
                    _batches.Update(batch);

                    // 记录批次溯源：这个订单的这件商品来自哪个批次
                    await _allocations.AddAsync(new LogFulfillmentBatchItem
                    {
                        DeliveryID = delivery.DeliveryID,
                        ProductID = item.ProductID,
                        BatchID = batch.BatchID,
                        Quantity = deduct
                    });

                    remaining -= deduct;
                }

                if (remaining > 0)
                    throw new InvalidOperationException($"商品 {item.ProductID} 可用批次库存不足");
            }

            await _uow.CommitAsync();
            return ApiResponse<LogExpressDelivery>.Success(delivery, "冷链发货成功，已记录批次溯源");
        }
        catch (Exception e)
        {
            await _uow.RollbackAsync();
            return ApiResponse<LogExpressDelivery>.Fail(e.Message);
        }
    }
}
