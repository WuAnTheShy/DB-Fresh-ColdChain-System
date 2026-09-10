using FreshColdChain.Interfaces;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// 供应商动态定价规则变更 → 同步已上架条目快照 的实现。
// 定价口径与入团（PromoterService.AddProductEntryAsync）保持一致：
//   动态定价 = 规则引擎按（商品×供应商×数量1×当前时间）计算，无规则命中时=货物售价；
//   推荐价 = 动态定价 × 1.2；
//   允许范围：|团长价 - 推荐价| &lt; |推荐价 - 动态定价| / 2。
// 超出范围的原团长定价按“两端向内缩至分”钳制到最近合法值，避免边界等于 allowedDiff
// （原校验为严格小于），保证日后再次提交时同样通过校验。
public class PromoterListedPriceSyncService : IPromoterListedPriceSyncService
{
    private readonly IPromoterProductRepository _productRepository;
    private readonly IPricingService _pricing;
    private readonly IUnitOfWork _uow;

    public PromoterListedPriceSyncService(
        IPromoterProductRepository productRepository,
        IPricingService pricing,
        IUnitOfWork uow)
    {
        _productRepository = productRepository;
        _pricing = pricing;
        _uow = uow;
    }

    public async Task<int> SyncListedPricesAsync(string supplierId, string productId)
    {
        if (string.IsNullOrWhiteSpace(supplierId) || string.IsNullOrWhiteSpace(productId))
            return 0;

        var entries = await _productRepository.GetActiveListedEntriesAsync(supplierId, productId);
        if (entries.Count == 0)
            return 0;

        // 先在事务外把每条的新快照与钳制后团长价都算出来：
        // 规则引擎内部查询不携带本服务事务，若在 BeginAsync 之后调用会破坏 Oracle 事务约束。
        var updates = new List<(string PromoterId, decimal SupplyPrice, decimal DefaultPrice, decimal PromoterPrice)>();
        foreach (var (promoterId, promoterPrice) in entries)
        {
            decimal supplyPrice;
            try
            {
                var calc = await _pricing.CalculatePriceAsync(new PriceCalculationRequest
                {
                    ProductID = productId,
                    SupplierID = supplierId,
                    Quantity = 1m // 与入团口径一致：单件询价
                });
                if (!calc.IsSuccess || calc.Data == null)
                    continue; // 引擎未给出结果时保留旧快照
                supplyPrice = calc.Data.FinalPrice;
            }
            catch
            {
                continue; // 规则引擎异常时保留旧快照，保证规则管理页面可用
            }

            var defaultPrice = Math.Round(supplyPrice * 1.2m, 2);
            var price = ClampToListedRange(promoterPrice ?? defaultPrice, defaultPrice, supplyPrice);
            updates.Add((promoterId, supplyPrice, defaultPrice, price));
        }

        if (updates.Count == 0)
            return 0;

        await _uow.BeginAsync();
        try
        {
            var updated = 0;
            foreach (var u in updates)
            {
                updated += await _productRepository.RefreshListedEntrySnapshotAsync(
                    u.PromoterId, productId, supplierId,
                    u.SupplyPrice, u.DefaultPrice, u.PromoterPrice, _uow.Transaction);
            }
            await _uow.CommitAsync();
            return updated;
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    // 团长定价越界钳制：合法区间为 (推荐价 - allowedDiff, 推荐价 + allowedDiff)，
    // 其中 allowedDiff = |推荐价 - 动态定价| / 2，端点不合法（原校验为严格小于）。
    // 取保留两位小数且严格落在区间内的最大偏移作为边界，退回默认时取推荐价。
    private static decimal ClampToListedRange(decimal price, decimal defaultPrice, decimal supplyPrice)
    {
        var allowedDiff = Math.Abs(defaultPrice - supplyPrice) / 2m;
        if (allowedDiff == 0m)
            return defaultPrice;

        var actualDiff = Math.Abs(price - defaultPrice);
        if (actualDiff < allowedDiff)
            return price;

        // 允许的最大“分”级偏移：比 allowedDiff 至少小 1 分钱（用极小量避免浮点/精度边界）
        var marginCents = Math.Max(0, (int)Math.Floor((allowedDiff - 0.000001m) * 100m));
        var margin = marginCents / 100m;

        var clamped = Math.Clamp(price, defaultPrice - margin, defaultPrice + margin);
        clamped = Math.Round(clamped, 2);

        // 边界兜底：任何情况下不落出合法范围，否则退回推荐价
        return Math.Abs(clamped - defaultPrice) >= allowedDiff ? defaultPrice : clamped;
    }
}
