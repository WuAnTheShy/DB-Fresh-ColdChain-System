using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;

namespace FreshGroupSystem.Services;

/// <summary>按温层重量计费，并在发货时按 FEFO 扣减批次、留下批次映射。</summary>
public class ColdChainLogisticsService : IColdChainLogisticsService
{
    private readonly IProductRepository _products;
    private readonly IStockSummaryRepository _stockSummary;
    private readonly IStockBatchRepository _batches;
    private readonly IBaseRepository<LogFreightTemplate> _templates;
    private readonly IBaseRepository<LogExpressDelivery> _deliveries;
    private readonly IBaseRepository<LogFulfillmentBatchItem> _allocations;
    private readonly IUnitOfWork _uow;
    public ColdChainLogisticsService(IProductRepository products, IStockSummaryRepository stockSummary,
        IStockBatchRepository batches, IBaseRepository<LogFreightTemplate> templates,
        IBaseRepository<LogExpressDelivery> deliveries, IBaseRepository<LogFulfillmentBatchItem> allocations,
        IUnitOfWork uow)
        => (_products, _stockSummary, _batches, _templates, _deliveries, _allocations, _uow)
            = (products, stockSummary, batches, templates, deliveries, allocations, uow);

    public async Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request)
    {
        if (request.Items.Count == 0) return ApiResponse<FreightQuoteDto>.Fail("至少需要一个商品");
        var rules = await _templates.GetAllAsync(); decimal total = 0;
        foreach (var item in request.Items)
        {
            var p = await _products.GetByIdAsync(item.ProductID);
            if (p == null || item.Quantity <= 0 || p.WeightKG is not > 0) return ApiResponse<FreightQuoteDto>.Fail("商品、数量或计费重量无效");
            var zone = string.IsNullOrWhiteSpace(p.StorageReq) ? "CHILLED" : p.StorageReq.ToUpperInvariant();
            var rule = rules.Where(x => x.IsEnabled == 1 && x.TemperatureZone == zone &&
                (x.DestinationProvince == "*" || x.DestinationProvince == request.Province) &&
                (x.DestinationCity == "*" || x.DestinationCity == request.City) &&
                (x.DestinationDistrict == "*" || x.DestinationDistrict == request.District))
                .OrderByDescending(x => (x.DestinationProvince != "*" ? 4 : 0) + (x.DestinationCity != "*" ? 2 : 0) + (x.DestinationDistrict != "*" ? 1 : 0)).FirstOrDefault();
            if (rule == null) return ApiResponse<FreightQuoteDto>.Fail($"缺少 {zone} 温层运费规则");
            if (rule.FreeShippingThreshold.HasValue && request.GoodsAmount >= rule.FreeShippingThreshold) continue;
            var weight = p.WeightKG.Value * item.Quantity;
            total += rule.BaseFee + Math.Max(0, decimal.Ceiling((weight - rule.BaseWeight) / rule.ExtraWeightUnit)) * rule.ExtraWeightFee + rule.PackagingFee;
        }
        return ApiResponse<FreightQuoteDto>.Success(new FreightQuoteDto { FreightAmount = decimal.Round(total, 2), RuleSummary = "按地区、温层、首重和续重计算" });
    }

    public async Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderID) || string.IsNullOrWhiteSpace(request.SupplierID) || request.Items.Count == 0)
            return ApiResponse<LogExpressDelivery>.Fail("发货参数不完整");

        try
        {
            await _uow.BeginAsync();

            var delivery = new LogExpressDelivery
            {
                OrderID = request.OrderID,
                SupplierID = request.SupplierID,
                TrackingNo = $"CC{Guid.NewGuid():N}"[..20]
            };
            await _deliveries.AddAsync(delivery);

            foreach (var item in request.Items)
            {
                // FOR UPDATE 行级锁：阻塞并发发货对同一产品库存的修改，防止超卖
                var stock = await _stockSummary.GetByProductIdForUpdateAsync(item.ProductID);
                if (stock == null)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存记录不存在");
                if (stock.AvailableQty < item.Quantity)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存不足（可用 {stock.AvailableQty}，需要 {item.Quantity}）");

                // FEFO 批次扣减
                var remain = item.Quantity;
                var batches = await _batches.GetByProductIdAsync(item.ProductID);
                foreach (var batch in batches.OrderBy(x => x.ExpiryDate))
                {
                    if (remain <= 0) break;
                    var qty = Math.Min(remain, batch.CurrentQty);
                    if (qty <= 0) continue;

                    batch.CurrentQty -= qty;
                    if (batch.CurrentQty == 0) batch.Status = "DEPLETED";
                    _batches.Update(batch);

                    // 精准溯源：记录发货 → 批次映射
                    await _allocations.AddAsync(new LogFulfillmentBatchItem
                    {
                        DeliveryID = delivery.DeliveryID,
                        ProductID = item.ProductID,
                        BatchID = batch.BatchID,
                        Quantity = qty
                    });
                    remain -= qty;
                }

                if (remain > 0)
                    throw new InvalidOperationException($"商品 {item.ProductID} 可用批次库存不足（缺 {remain}）");

                // 同步更新库存汇总
                stock.TotalQty -= item.Quantity;
                stock.AvailableQty = stock.TotalQty - stock.LockedQty;
                stock.UpdateTime = DateTime.Now;
                _stockSummary.Update(stock);
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
