using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models;
using FreshGroupSystem.Models.DTOs;
using FreshGroupSystem.Repositories;

namespace FreshGroupSystem.Services;

/// <summary>
/// A组冷链物流服务：阶梯运费报价 + FEFO批次发货 + 精准溯源
/// 负责三张 Log_* 表（Log_FreightTemplates / Log_ExpressDeliveries / Log_FulfillmentBatchItems）
/// 发货时需同步扣减共享表 Inv_StockSummary（库存汇总），保证数据一致性
/// </summary>
public class ColdChainLogisticsService : IColdChainLogisticsService
{
    // 共享仓储（A组其他同学维护）：只读商品信息 + 库存汇总读写 + 批次读写
    private readonly IProductRepository _products;
    private readonly IStockSummaryRepository _stockSummary;  // P0修复新增：发货需同步更新库存汇总
    private readonly IStockBatchRepository _batches;
    // 冷链专属仓储（暂用泛型，后续将抽取专属接口）
    private readonly IBaseRepository<LogFreightTemplate> _templates;
    private readonly IBaseRepository<LogExpressDelivery> _deliveries;
    private readonly IBaseRepository<LogFulfillmentBatchItem> _allocations;
    // 工作单元：一次请求共享同一连接和事务
    private readonly IUnitOfWork _uow;

    public ColdChainLogisticsService(IProductRepository products, IStockSummaryRepository stockSummary,
        IStockBatchRepository batches, IBaseRepository<LogFreightTemplate> templates,
        IBaseRepository<LogExpressDelivery> deliveries, IBaseRepository<LogFulfillmentBatchItem> allocations,
        IUnitOfWork uow)
        => (_products, _stockSummary, _batches, _templates, _deliveries, _allocations, _uow)
            = (products, stockSummary, batches, templates, deliveries, allocations, uow);

    /// <summary>
    /// 冷链阶梯运费报价
    /// 计费逻辑：
    ///   1. 根据商品 StorageReq（温区）和目的地（省→市→区三级匹配）查找运费模板
    ///   2. 模板匹配优先级：精确到区(4分) > 精确到市(2分) > 精确到省(1分) > 通配符*(0分)
    ///   3. 费用 = 首重费 + 续重费 × ceil((总重-首重)/续重单位) + 包装费
    ///   4. 单笔订单货值达到免运费阈值时免收运费
    /// </summary>
    public async Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request)
    {
        if (request.Items.Count == 0) return ApiResponse<FreightQuoteDto>.Fail("至少需要一个商品");

        // 加载全部启用的运费模板（数量少，全量加载做内存匹配）
        var rules = await _templates.GetAllAsync();
        decimal total = 0;

        foreach (var item in request.Items)
        {
            // 1. 校验商品信息 + 计费重量
            var p = await _products.GetByIdAsync(item.ProductID);
            if (p == null || item.Quantity <= 0 || p.WeightKG is not > 0)
                return ApiResponse<FreightQuoteDto>.Fail("商品、数量或计费重量无效");

            // 2. 确定温区：默认 CHILLED（冷藏），读取商品 StorageReq 字段
            var zone = string.IsNullOrWhiteSpace(p.StorageReq) ? "CHILLED" : p.StorageReq.ToUpperInvariant();

            // 3. 匹配运费模板：省/市/区三级匹配，越精确优先级越高
            var rule = rules.Where(x => x.IsEnabled == 1 && x.TemperatureZone == zone &&
                (x.DestinationProvince == "*" || x.DestinationProvince == request.Province) &&
                (x.DestinationCity == "*" || x.DestinationCity == request.City) &&
                (x.DestinationDistrict == "*" || x.DestinationDistrict == request.District))
                .OrderByDescending(x => (x.DestinationProvince != "*" ? 4 : 0)
                                      + (x.DestinationCity != "*" ? 2 : 0)
                                      + (x.DestinationDistrict != "*" ? 1 : 0))
                .FirstOrDefault();

            if (rule == null)
                return ApiResponse<FreightQuoteDto>.Fail($"缺少 {zone} 温层运费规则");

            // 4. 免运费阈值：货值达标则本商品不参与计费
            if (rule.FreeShippingThreshold.HasValue && request.GoodsAmount >= rule.FreeShippingThreshold)
                continue;

            // 5. 阶梯计费：首重费 + 续重费 × 续重阶梯数 + 包装费
            var weight = p.WeightKG.Value * item.Quantity;
            total += rule.BaseFee
                   + Math.Max(0, decimal.Ceiling((weight - rule.BaseWeight) / rule.ExtraWeightUnit)) * rule.ExtraWeightFee
                   + rule.PackagingFee;
        }

        return ApiResponse<FreightQuoteDto>.Success(new FreightQuoteDto
        {
            FreightAmount = decimal.Round(total, 2),
            RuleSummary = "按地区、温层、首重和续重计算"
        });
    }

    /// <summary>
    /// 冷链发货履约：一个订单按供应商拆分一张发货单，FEFO 扣减批次 + 写入溯源映射
    /// P0修复（2026-08-02）：
    ///   1. 加入 FOR UPDATE 行级锁，阻塞并发发货对同一产品的库存修改，防止超卖
    ///   2. 扣减批次后同步更新 Inv_StockSummary.TotalQty/AvailableQty，修复库存汇总不一致
    /// 事务保护：库存不足或批次不足时自动回滚，发货单和溯源记录不会残留
    /// </summary>
    public async Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderID) || string.IsNullOrWhiteSpace(request.SupplierID) || request.Items.Count == 0)
            return ApiResponse<LogExpressDelivery>.Fail("发货参数不完整");

        try
        {
            // 1. 开启事务 — 后续所有操作在同一事务内，失败整体回滚
            await _uow.BeginAsync();

            // 2. 创建冷链发货单（一个供应商一张单）
            var delivery = new LogExpressDelivery
            {
                OrderID = request.OrderID,
                SupplierID = request.SupplierID,
                TrackingNo = $"CC{Guid.NewGuid():N}"[..20]
            };
            await _deliveries.AddAsync(delivery);

            // 3. 逐商品处理：加锁 → 校验库存 → FEFO 扣批次 → 更新汇总 → 写溯源
            foreach (var item in request.Items)
            {
                // 3a. SELECT ... FOR UPDATE：行级锁，阻塞并发请求对同一产品库存的修改
                var stock = await _stockSummary.GetByProductIdForUpdateAsync(item.ProductID);
                if (stock == null)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存记录不存在");
                if (stock.AvailableQty < item.Quantity)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存不足（可用 {stock.AvailableQty}，需要 {item.Quantity}）");

                // 3b. FEFO 先进先出：按过期时间从早到晚扣减库存批次
                var remain = item.Quantity;
                var batches = await _batches.GetByProductIdAsync(item.ProductID);
                foreach (var batch in batches.OrderBy(x => x.ExpiryDate))
                {
                    if (remain <= 0) break;
                    var qty = Math.Min(remain, batch.CurrentQty);
                    if (qty <= 0) continue;

                    batch.CurrentQty -= qty;
                    if (batch.CurrentQty == 0) batch.Status = "DEPLETED"; // 批次耗尽标记
                    _batches.Update(batch);

                    // 3c. 精准溯源：记录本次发货用了哪个批次的多少货
                    await _allocations.AddAsync(new LogFulfillmentBatchItem
                    {
                        DeliveryID = delivery.DeliveryID,
                        ProductID = item.ProductID,
                        BatchID = batch.BatchID,
                        Quantity = qty
                    });
                    remain -= qty;
                }

                // 3d. 批次库存不足以满足需求时抛出异常，触发事务回滚
                if (remain > 0)
                    throw new InvalidOperationException($"商品 {item.ProductID} 可用批次库存不足（缺 {remain}）");

                // 3e. 同步更新库存汇总 — 保证 Inv_StockSummary 与 Inv_StockBatches 汇总一致
                stock.TotalQty -= item.Quantity;
                stock.AvailableQty = stock.TotalQty - stock.LockedQty;
                stock.UpdateTime = DateTime.Now;
                _stockSummary.Update(stock);
            }

            // 4. 全部成功，提交事务
            await _uow.CommitAsync();
            return ApiResponse<LogExpressDelivery>.Success(delivery, "冷链发货成功，已记录批次溯源");
        }
        catch (Exception e)
        {
            // 任一步骤失败 → 回滚事务，不残留任何数据
            await _uow.RollbackAsync();
            return ApiResponse<LogExpressDelivery>.Fail(e.Message);
        }
    }
}
