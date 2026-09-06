using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

public sealed class ProductEvaluationService(
    IOrderRepository orderRepository,
    IProductEvaluationRepository evaluationRepository,
    IOrderTransactionManager transactionManager) : IProductEvaluationService
{
    public async Task SubmitAsync(
        string orderId,
        string orderDetailId,
        string customerId,
        ProductEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!GroupBIds.IsValid(orderId) || !GroupBIds.IsValid(orderDetailId) || !GroupBIds.IsValid(customerId))
            throw new GroupBBusinessException("评价参数不正确");

        var selected = (request.Dimensions ?? [])
            .Select(value => value?.Trim().ToUpperInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (selected.Count == 0)
            throw new GroupBBusinessException("请至少选择一项评价");
        if (selected.Any(code => !ProductEvaluationDimensions.Names.ContainsKey(code)))
            throw new GroupBBusinessException("包含不支持的评价维度");

        cancellationToken.ThrowIfCancellationRequested();
        await transactionManager.ExecuteAsync(async transaction =>
        {
            var order = await orderRepository.GetByIdForUpdateAsync(orderId, transaction)
                ?? throw new GroupBBusinessException("订单不存在");
            if (!string.Equals(order.CustomerId, customerId, StringComparison.Ordinal))
                throw new GroupBBusinessException("无权评价其他消费者的订单");

            var detail = (await orderRepository.GetDetailsAsync(orderId, transaction))
                .SingleOrDefault(item => string.Equals(item.OrderDetailId, orderDetailId, StringComparison.Ordinal))
                ?? throw new GroupBBusinessException("订单商品不存在");
            if (!string.Equals(detail.ReceiptStatus, "RECEIVED", StringComparison.Ordinal))
                throw new GroupBBusinessException("确认收货后才能评价");
            if (string.IsNullOrWhiteSpace(order.PromoterId))
                throw new GroupBBusinessException("订单缺少团长信息，暂时无法评价");
            if (await evaluationRepository.ExistsForOrderDetailAsync(orderDetailId, transaction))
                throw new GroupBBusinessException("该商品已完成评价");

            await evaluationRepository.InsertAsync(new ProductEvaluation
            {
                EvaluationId = GroupBIds.NewId(),
                OrderDetailId = detail.OrderDetailId,
                OrderId = order.OrderId,
                ProductId = detail.ProductId,
                PromoterId = order.PromoterId,
                CustomerId = order.CustomerId,
                HighQuality = Flag(ProductEvaluationDimensions.HighQuality),
                FastShipping = Flag(ProductEvaluationDimensions.FastShipping),
                GoodPackaging = Flag(ProductEvaluationDimensions.GoodPackaging),
                CostEffective = Flag(ProductEvaluationDimensions.CostEffective),
                Affordable = Flag(ProductEvaluationDimensions.Affordable),
                ReliablePromoter = Flag(ProductEvaluationDimensions.ReliablePromoter)
            }, transaction);

            int Flag(string code) => selected.Contains(code) ? 1 : 0;
        });
    }

    public async Task<ProductEvaluationSummary> GetSummaryAsync(string promoterId)
    {
        if (!GroupBIds.IsValid(promoterId))
            throw new GroupBBusinessException("团长参数不正确");

        var aggregate = await evaluationRepository.GetSummaryAsync(promoterId.Trim());
        return new ProductEvaluationSummary
        {
            TotalCount = aggregate.TotalCount,
            Dimensions =
            [
                Item(ProductEvaluationDimensions.HighQuality, aggregate.HighQualityCount),
                Item(ProductEvaluationDimensions.FastShipping, aggregate.FastShippingCount),
                Item(ProductEvaluationDimensions.GoodPackaging, aggregate.GoodPackagingCount),
                Item(ProductEvaluationDimensions.CostEffective, aggregate.CostEffectiveCount),
                Item(ProductEvaluationDimensions.Affordable, aggregate.AffordableCount),
                Item(ProductEvaluationDimensions.ReliablePromoter, aggregate.ReliablePromoterCount)
            ]
        };
    }

    public Task<HashSet<string>> GetEvaluatedOrderDetailIdsAsync(IEnumerable<string> orderDetailIds) =>
        evaluationRepository.GetEvaluatedOrderDetailIdsAsync(orderDetailIds);

    private static ProductEvaluationDimensionSummary Item(string code, int count) =>
        new(code, ProductEvaluationDimensions.Names[code], count);
}
