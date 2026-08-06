using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

/// <summary>
/// 物流适配器 — 实现 B 组的 ILogisticsService 接口
/// 复用 A 组 ColdChainLogisticsService 的阶梯运费和 FEFO 发货逻辑
/// </summary>
public class LogisticsServiceAdapter : ILogisticsService
{
    private readonly IUnitOfWork _uow;
    private readonly IProductRepository _productRepo;
    private readonly IStockSummaryRepository _stockSummaryRepo;
    private readonly IStockBatchRepository _batchRepo;
    private readonly ILogFreightTemplateRepository _templateRepo;
    private readonly ILogExpressDeliveryRepository _deliveryRepo;
    private readonly IBaseRepository<LogFulfillmentBatchItem> _allocationRepo;

    public LogisticsServiceAdapter(
        IUnitOfWork uow,
        IProductRepository productRepo,
        IStockSummaryRepository stockSummaryRepo,
        IStockBatchRepository batchRepo,
        ILogFreightTemplateRepository templateRepo,
        ILogExpressDeliveryRepository deliveryRepo,
        IBaseRepository<LogFulfillmentBatchItem> allocationRepo)
    {
        _uow = uow;
        _productRepo = productRepo;
        _stockSummaryRepo = stockSummaryRepo;
        _batchRepo = batchRepo;
        _templateRepo = templateRepo;
        _deliveryRepo = deliveryRepo;
        _allocationRepo = allocationRepo;
    }

    /// <summary>
    /// 阶梯冷链运费计算（按温区、重量、目的地匹配规则）
    /// </summary>
    public async Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);

        _uow.AttachExternalTransaction(transaction);

        if (request.Items.Count == 0) return 0m;

        var rules = await _templateRepo.GetEnabledAsync();
        decimal total = 0m;

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = await _productRepo.GetByIdAsync(item.ProductId);
            if (product == null || item.Quantity <= 0 || product.WeightKG is not > 0)
                continue;

            var zone = string.IsNullOrWhiteSpace(product.StorageReq)
                ? "CHILLED" : product.StorageReq.ToUpperInvariant();

            var matchedRule = rules
                .Where(r => r.IsEnabled == 1 && r.TemperatureZone == zone
                    && (r.DestinationProvince == "*" || r.DestinationProvince == request.Province)
                    && (r.DestinationCity == "*" || r.DestinationCity == request.City)
                    && (r.DestinationDistrict == "*" || r.DestinationDistrict == request.District))
                .OrderByDescending(r =>
                    (r.DestinationProvince != "*" ? 4 : 0)
                    + (r.DestinationCity != "*" ? 2 : 0)
                    + (r.DestinationDistrict != "*" ? 1 : 0))
                .FirstOrDefault();

            if (matchedRule == null) continue;

            if (matchedRule.FreeShippingThreshold.HasValue
                && request.GoodsAmount >= matchedRule.FreeShippingThreshold)
                continue;

            var weight = product.WeightKG.Value * item.Quantity;
            var extraUnits = matchedRule.ExtraWeightUnit > 0
                ? Math.Max(0, decimal.Ceiling((weight - matchedRule.BaseWeight) / matchedRule.ExtraWeightUnit))
                : 0;
            total += matchedRule.BaseFee
                + extraUnits * matchedRule.ExtraWeightFee
                + matchedRule.PackagingFee;
        }

        return decimal.Round(total, 2);
    }

    /// <summary>
    /// FEFO 批次扣减 + 创建物流发货单 + 批次溯源记录 + 同步库存汇总
    /// </summary>
    public async Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);

        _uow.AttachExternalTransaction(transaction);

        // 从首个商品获取供应商 ID
        var supplierId = request.Items.FirstOrDefault()?.SupplierId ?? string.Empty;

        // 创建发货单
        var delivery = new LogExpressDelivery
        {
            OrderID = request.OrderId,
            SupplierID = supplierId,
            TrackingNo = $"CC{Guid.NewGuid():N}"[..20],
            LogisticsStatus = "SHIPPED",
            ShippedAt = DateTime.Now
        };
        await _deliveryRepo.AddAsync(delivery);

        // 逐商品按 FEFO 扣减批次
        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // FOR UPDATE 行级锁，阻塞并发请求对同一产品库存的修改
            var stock = await _stockSummaryRepo.GetByProductIdForUpdateAsync(item.ProductId);
            if (stock == null)
                throw new InvalidOperationException($"商品 {item.ProductId} 库存记录不存在");
            if (stock.AvailableQty < item.Quantity)
                throw new InvalidOperationException($"商品 {item.ProductId}({item.ProductName}) 库存不足（可用 {stock.AvailableQty}，需要 {item.Quantity}）");

            var remaining = item.Quantity;
            var batches = await _batchRepo.GetByProductIdAsync(item.ProductId);

            foreach (var batch in batches.OrderBy(b => b.ExpiryDate))
            {
                if (remaining <= 0) break;

                var deduct = Math.Min(remaining, batch.CurrentQty);
                if (deduct <= 0) continue;

                batch.CurrentQty -= deduct;
                if (batch.CurrentQty == 0) batch.Status = "DEPLETED";
                _batchRepo.Update(batch);

                await _allocationRepo.AddAsync(new LogFulfillmentBatchItem
                {
                    DeliveryID = delivery.DeliveryID,
                    ProductID = item.ProductId,
                    BatchID = batch.BatchID,
                    Quantity = deduct
                });

                remaining -= deduct;
            }

            if (remaining > 0)
                throw new InvalidOperationException(
                    $"商品 {item.ProductId}({item.ProductName}) 可用批次库存不足，还差 {remaining}");

            // 同步更新库存汇总：扣减总量并释放已锁定的预留量
            stock.TotalQty -= item.Quantity;
            stock.LockedQty = Math.Max(0, stock.LockedQty - item.Quantity);
            stock.AvailableQty = stock.TotalQty - stock.LockedQty;
            stock.UpdateTime = DateTime.Now;
            _stockSummaryRepo.Update(stock);
        }
    }

    /// <summary>
    /// 查询供应商履约状态（只读，不需要事务）
    /// </summary>
    public async Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        // 只读查询，不挂载外部事务
        var orderDeliveries = await _deliveryRepo.GetByOrderIdAsync(orderId);

        return supplierIds.Select(sid =>
        {
            var match = orderDeliveries.FirstOrDefault(d => d.SupplierID == sid);
            return new SupplierFulfillmentStatus
            {
                SupplierId = sid,
                StatusName = match?.LogisticsStatus ?? "未发货",
                TrackingNo = match?.TrackingNo
            };
        }).ToList();
    }
}
