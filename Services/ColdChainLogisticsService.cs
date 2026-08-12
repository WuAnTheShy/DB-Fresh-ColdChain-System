using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// A组冷链物流服务：阶梯运费报价 + FEFO批次发货 + 精准溯源
/// 负责三张 Log_* 表（Log_FreightTemplates / Log_ExpressDeliveries / Log_FulfillmentBatchItems）
/// 发货时需同步扣减共享表 Inv_StockSummary（库存汇总），保证数据一致性
/// </summary>
public class ColdChainLogisticsService : IColdChainLogisticsService
{
    private readonly IProductRepository _products;
    private readonly IStockSummaryRepository _stockSummary;
    private readonly IStockBatchRepository _batches;
    private readonly ILogFreightTemplateRepository _templates;
    private readonly ILogExpressDeliveryRepository _deliveries;
    private readonly ILogFulfillmentBatchItemRepository _allocations;
    // 工作单元：一次请求共享同一连接和事务
    private readonly IUnitOfWork _uow;

    public ColdChainLogisticsService(
        IProductRepository products,
        IStockSummaryRepository stockSummary,
        IStockBatchRepository batches,
        ILogFreightTemplateRepository templates,
        ILogExpressDeliveryRepository deliveries,
        ILogFulfillmentBatchItemRepository allocations,
        IUnitOfWork uow)
    {
        _products = products;
        _stockSummary = stockSummary;
        _batches = batches;
        _templates = templates;
        _deliveries = deliveries;
        _allocations = allocations;
        _uow = uow;
    }

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
        if (request.Items.Count == 0)
            return ApiResponse<FreightQuoteDto>.Fail("至少需要一个商品");

        // 只加载启用的运费模板，在内存中按地区优先级匹配
        var rules = await _templates.GetEnabledAsync();
        decimal total = 0;

        foreach (var item in request.Items)
        {
            // 1. 校验商品信息 + 计费重量
            var product = await _products.GetByIdAsync(item.ProductID);
            if (product == null || item.Quantity <= 0 || product.WeightKG is not > 0)
                return ApiResponse<FreightQuoteDto>.Fail("商品、数量或计费重量无效");

            // 2. 确定温区：默认 CHILLED（冷藏），读取商品 StorageReq 字段
            var zone = string.IsNullOrWhiteSpace(product.StorageReq)
                ? "CHILLED"
                : product.StorageReq.ToUpperInvariant();

            // 3. 按地区优先级匹配运费规则（省 > 市 > 区 > 通配 *）
            //    空字符串/NULL 视为通配 *，TemperatureZone 为空时匹配所有温层
            var matchedRule = rules
                .Where(r => r.IsEnabled == 1
                    && (string.IsNullOrWhiteSpace(r.TemperatureZone) || r.TemperatureZone == zone)
                    && (IsWildcard(r.DestinationProvince) || r.DestinationProvince == request.Province)
                    && (IsWildcard(r.DestinationCity) || r.DestinationCity == request.City)
                    && (IsWildcard(r.DestinationDistrict) || r.DestinationDistrict == request.District))
                .OrderByDescending(r =>
                    (!IsWildcard(r.DestinationProvince) ? 4 : 0)
                    + (!IsWildcard(r.DestinationCity) ? 2 : 0)
                    + (!IsWildcard(r.DestinationDistrict) ? 1 : 0))
                .FirstOrDefault();

            if (matchedRule == null)
                return ApiResponse<FreightQuoteDto>.Fail($"缺少 {zone} 温层运费规则");

            // 4. 免运费阈值：货值达标则本商品不参与计费
            if (matchedRule.FreeShippingThreshold.HasValue
                && request.GoodsAmount >= matchedRule.FreeShippingThreshold)
                continue;

            // 5. 阶梯计费：首重费 + 续重费 × 续重阶梯数 + 包装费
            var weight = product.WeightKG.Value * item.Quantity;
            var extraUnits = matchedRule.ExtraWeightUnit > 0
                ? Math.Max(0, decimal.Ceiling((weight - matchedRule.BaseWeight) / matchedRule.ExtraWeightUnit))
                : 0;
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
    /// 冷链发货履约：一个订单按供应商拆分一张发货单，FEFO 扣减批次 + 写入溯源映射。
    /// 使用 FOR UPDATE 行级锁防止并发超卖，事务内同步更新 Inv_StockSummary 保证库存一致性。
    /// </summary>
    public async Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderID)
            || string.IsNullOrWhiteSpace(request.SupplierID)
            || request.Items.Count == 0)
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
                if (item.Quantity <= 0)
                    throw new InvalidOperationException($"商品 {item.ProductID} 数量必须大于 0");
                // 3a. SELECT ... FOR UPDATE：行级锁，阻塞并发请求对同一产品库存的修改
                var stock = await _stockSummary.GetByProductIdForUpdateAsync(item.ProductID);
                if (stock == null)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存记录不存在");
                if (stock.AvailableQty < item.Quantity)
                    throw new InvalidOperationException($"商品 {item.ProductID} 库存不足（可用 {stock.AvailableQty}，需要 {item.Quantity}）");

                // 3b. FEFO 先进先出：按过期时间从早到晚扣减库存批次（SKIP LOCKED 防并发冲突）
                var remaining = item.Quantity;
                var batches = await _batches.GetByProductIdForUpdateAsync(item.ProductID);

                foreach (var batch in batches)
                {
                    if (remaining <= 0) break;

                    var deduct = Math.Min(remaining, batch.CurrentQty);
                    if (deduct <= 0) continue;

                    batch.CurrentQty -= deduct;
                    if (batch.CurrentQty == 0)
                        batch.Status = "DEPLETED"; // 批次耗尽标记
                    _batches.Update(batch);

                    // 3c. 精准溯源：记录本次发货用了哪个批次的多少货
                    await _allocations.AddAsync(new LogFulfillmentBatchItem
                    {
                        DeliveryID = delivery.DeliveryID,
                        ProductID = item.ProductID,
                        BatchID = batch.BatchID,
                        Quantity = deduct
                    });

                    remaining -= deduct;
                }

                // 3d. 批次库存不足以满足需求时抛出异常，触发事务回滚
                if (remaining > 0)
                    throw new InvalidOperationException($"商品 {item.ProductID} 可用批次库存不足（缺 {remaining}）");

                // 3e. 同步更新库存汇总 — 扣减总量并释放已锁定的预留量
                stock.TotalQty -= item.Quantity;
                stock.LockedQty = Math.Max(0, stock.LockedQty - item.Quantity);
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

    // ========== 精准溯源查询 ==========

    /// <summary>
    /// 按订单 ID 查询溯源链路：该订单 → 所有发货单 → 每单用了哪些批次
    /// </summary>
    public async Task<ApiResponse<List<DeliveryTraceDto>>> GetTraceabilityByOrderAsync(string orderId)
    {
        var deliveries = await _deliveries.GetByOrderIdAsync(orderId);
        if (deliveries.Count == 0)
            return ApiResponse<List<DeliveryTraceDto>>.Fail("未找到该订单的发货记录", 404);

        var result = new List<DeliveryTraceDto>();
        foreach (var d in deliveries)
        {
            var trace = await BuildDeliveryTraceAsync(d);
            result.Add(trace);
        }

        return ApiResponse<List<DeliveryTraceDto>>.Success(result);
    }

    /// <summary>
    /// 按发货单 ID 查询单张发货单的批次溯源明细
    /// </summary>
    public async Task<ApiResponse<DeliveryTraceDto>> GetTraceabilityByDeliveryAsync(string deliveryId)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId);
        if (delivery == null)
            return ApiResponse<DeliveryTraceDto>.Fail("发货单不存在", 404);

        var trace = await BuildDeliveryTraceAsync(delivery);
        return ApiResponse<DeliveryTraceDto>.Success(trace);
    }

    /// <summary>
    /// 反向溯源：按批次 ID 查询该批次被哪些发货单使用
    /// </summary>
    public async Task<ApiResponse<BatchTraceDto>> GetBatchTraceAsync(string batchId)
    {
        var batch = await _batches.GetByIdAsync(batchId);
        if (batch == null)
            return ApiResponse<BatchTraceDto>.Fail("批次不存在", 404);

        // 查询该批次的所有扣减记录
        var items = await _allocations.GetByBatchIdAsync(batchId);

        // 批量加载关联数据：所有涉及的发货单（去重）和产品（同一批次只有一个产品）
        var deliveryIds = items.Select(i => i.DeliveryID).Distinct().ToList();
        var deliveries = new Dictionary<string, LogExpressDelivery?>();
        foreach (var did in deliveryIds)
            deliveries[did] = await _deliveries.GetByIdAsync(did);

        var product = await _products.GetByIdAsync(batch.ProductID);

        var allocations = new List<BatchAllocationDto>();
        foreach (var item in items)
        {
            deliveries.TryGetValue(item.DeliveryID, out var delivery);
            allocations.Add(new BatchAllocationDto
            {
                AllocationID = item.AllocationID,
                ProductID = item.ProductID,
                ProductName = product?.ProductName ?? "",
                BatchID = item.BatchID,
                BatchNo = batch.BatchNo,
                ExpiryDate = batch.ExpiryDate,
                Quantity = item.Quantity,
                DeliveryID = item.DeliveryID,
                TrackingNo = delivery?.TrackingNo ?? ""
            });
        }

        return ApiResponse<BatchTraceDto>.Success(new BatchTraceDto
        {
            BatchID = batch.BatchID,
            BatchNo = batch.BatchNo,
            ProductID = batch.ProductID,
            ProductName = product?.ProductName ?? "",
            ExpiryDate = batch.ExpiryDate,
            Allocations = allocations
        });
    }

    // ========== 溯源辅助方法 ==========

    /// <summary>判断地区字段是否为通配符（* 或空或 NULL）</summary>
    private static bool IsWildcard(string? val)
        => string.IsNullOrWhiteSpace(val) || val == "*";

    /// <summary>
    /// 构建一张发货单的完整溯源链路：发货单信息 + 每件商品从哪些批次扣减
    /// </summary>
    private async Task<DeliveryTraceDto> BuildDeliveryTraceAsync(LogExpressDelivery delivery)
    {
        var items = await _allocations.GetByDeliveryIdAsync(delivery.DeliveryID);

        // 批量加载关联数据，避免 N+1 查询
        var productIds = items.Select(i => i.ProductID).Distinct().ToList();
        var batchIds = items.Select(i => i.BatchID).Distinct().ToList();

        var productMap = new Dictionary<string, InvProduct?>();
        foreach (var pid in productIds)
            productMap[pid] = await _products.GetByIdAsync(pid);

        var batchMap = new Dictionary<string, InvStockBatch?>();
        foreach (var bid in batchIds)
            batchMap[bid] = await _batches.GetByIdAsync(bid);

        var allocations = new List<BatchAllocationDto>();
        foreach (var item in items)
        {
            productMap.TryGetValue(item.ProductID, out var product);
            batchMap.TryGetValue(item.BatchID, out var batch);

            allocations.Add(new BatchAllocationDto
            {
                AllocationID = item.AllocationID,
                ProductID = item.ProductID,
                ProductName = product?.ProductName ?? "",
                BatchID = item.BatchID,
                BatchNo = batch?.BatchNo ?? "",
                ExpiryDate = batch?.ExpiryDate,
                Quantity = item.Quantity,
                DeliveryID = delivery.DeliveryID,
                TrackingNo = delivery.TrackingNo
            });
        }

        return new DeliveryTraceDto
        {
            DeliveryID = delivery.DeliveryID,
            OrderID = delivery.OrderID,
            SupplierID = delivery.SupplierID,
            TrackingNo = delivery.TrackingNo,
            LogisticsStatus = delivery.LogisticsStatus,
            ShippedAt = delivery.ShippedAt,
            Allocations = allocations
        };
    }
}
