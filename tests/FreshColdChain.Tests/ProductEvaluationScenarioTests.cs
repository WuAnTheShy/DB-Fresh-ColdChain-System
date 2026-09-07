using System.Data;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class ProductEvaluationScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        try
        {
            await ReceivedItemCanBeEvaluatedOnceAsync();
            await WholeOrderEvaluationIncludesEveryProductAsync();
            await UnreceivedItemCannotBeEvaluatedAsync();
            await SummaryIncludesAllProductsOfPromoterAsync();
            Console.WriteLine("PASS 已收货商品可提交一次多维度评价");
            Console.WriteLine("PASS 团长评价汇总包含其全部商品且隔离其他团长");
            Console.WriteLine("PASS 整单评价一次覆盖全部商品");
            Console.WriteLine("评价场景总数: 4, 通过: 4, 失败: 0");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL 商品评价场景");
            Console.WriteLine(exception);
            return 1;
        }
    }

    private static async Task ReceivedItemCanBeEvaluatedOnceAsync()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(Order());
        orders.Details.Add(Detail("RECEIVED"));
        var evaluations = new FakeEvaluationRepository();
        var service = new ProductEvaluationService(orders, evaluations, new FakeTransactionManager());

        await service.SubmitAsync(TestIds.Order, "detail-1", TestIds.Customer,
            new ProductEvaluationRequest
            {
                Dimensions = [ProductEvaluationDimensions.HighQuality, ProductEvaluationDimensions.GoodPackaging]
            });

        AssertEx.Equal(1, evaluations.Items.Count);
        AssertEx.Equal(1, evaluations.Items[0].HighQuality);
        AssertEx.Equal(1, evaluations.Items[0].GoodPackaging);
        AssertEx.Equal(0, evaluations.Items[0].FastShipping);

        await AssertEx.ThrowsAsync<GroupBBusinessException>(() => service.SubmitAsync(
            TestIds.Order, "detail-1", TestIds.Customer,
            new ProductEvaluationRequest { Dimensions = [ProductEvaluationDimensions.Affordable] }));
    }

    private static async Task UnreceivedItemCannotBeEvaluatedAsync()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(Order());
        orders.Details.Add(Detail("PENDING"));
        var service = new ProductEvaluationService(
            orders, new FakeEvaluationRepository(), new FakeTransactionManager());

        await AssertEx.ThrowsAsync<GroupBBusinessException>(() => service.SubmitAsync(
            TestIds.Order, "detail-1", TestIds.Customer,
            new ProductEvaluationRequest { Dimensions = [ProductEvaluationDimensions.HighQuality] }));
    }

    private static async Task WholeOrderEvaluationIncludesEveryProductAsync()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(Order());
        orders.Details.Add(Detail("RECEIVED"));
        orders.Details.Add(new BizOrderDetail
        {
            OrderDetailId = "detail-2",
            OrderId = TestIds.Order,
            ProductId = "product-2",
            ProductName = "测试商品2",
            ReceiptStatus = "RECEIVED"
        });
        var evaluations = new FakeEvaluationRepository();
        var service = new ProductEvaluationService(orders, evaluations, new FakeTransactionManager());

        await service.SubmitOrderAsync(TestIds.Order, TestIds.Customer,
            new ProductEvaluationRequest { Dimensions = [ProductEvaluationDimensions.FastShipping] });

        AssertEx.Equal(2, evaluations.Items.Count);
        AssertEx.True(evaluations.Items.All(item => item.FastShipping == 1));
        AssertEx.Equal(2, evaluations.Items.Select(item => item.OrderDetailId).Distinct().Count());
    }

    private static async Task SummaryIncludesAllProductsOfPromoterAsync()
    {
        var evaluations = new FakeEvaluationRepository();
        evaluations.Items.AddRange(
        [
            new ProductEvaluation { ProductId = "product-1", PromoterId = "promoter-1", HighQuality = 1 },
            new ProductEvaluation { ProductId = "product-2", PromoterId = "promoter-1", Affordable = 1 },
            new ProductEvaluation { ProductId = "product-3", PromoterId = "promoter-2", HighQuality = 1 }
        ]);
        var service = new ProductEvaluationService(
            new FakeOrderRepository(), evaluations, new FakeTransactionManager());

        var summary = await service.GetSummaryAsync("promoter-1");

        AssertEx.Equal(2, summary.TotalCount);
        AssertEx.Equal(1, summary.Dimensions.Single(item =>
            item.Code == ProductEvaluationDimensions.HighQuality).Count);
        AssertEx.Equal(1, summary.Dimensions.Single(item =>
            item.Code == ProductEvaluationDimensions.Affordable).Count);
    }

    private static BizOrder Order() => new()
    {
        OrderId = TestIds.Order,
        CustomerId = TestIds.Customer,
        PromoterId = "promoter-1"
    };

    private static BizOrderDetail Detail(string receiptStatus) => new()
    {
        OrderDetailId = "detail-1",
        OrderId = TestIds.Order,
        ProductId = "product-1",
        ProductName = "测试商品",
        ReceiptStatus = receiptStatus
    };

    private sealed class FakeEvaluationRepository : IProductEvaluationRepository
    {
        public List<ProductEvaluation> Items { get; } = [];

        public Task<bool> ExistsForOrderDetailAsync(string orderDetailId, IDbTransaction? transaction = null) =>
            Task.FromResult(Items.Any(item => item.OrderDetailId == orderDetailId));

        public Task InsertAsync(ProductEvaluation evaluation, IDbTransaction transaction)
        {
            if (transaction is FakeOrderTransaction fake) fake.Stage(() => Items.Add(evaluation));
            else Items.Add(evaluation);
            return Task.CompletedTask;
        }

        public Task<HashSet<string>> GetEvaluatedOrderDetailIdsAsync(
            IEnumerable<string> orderDetailIds,
            IDbTransaction? transaction = null)
        {
            var requested = orderDetailIds.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(Items.Where(item => requested.Contains(item.OrderDetailId))
                .Select(item => item.OrderDetailId).ToHashSet(StringComparer.Ordinal));
        }

        public Task<IReadOnlyList<ProductEvaluation>> GetByOrderDetailIdsAsync(
            IEnumerable<string> orderDetailIds,
            IDbTransaction? transaction = null)
        {
            var requested = orderDetailIds.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<ProductEvaluation>>(
                Items.Where(item => requested.Contains(item.OrderDetailId)).ToList());
        }

        public Task<ProductEvaluationAggregate> GetSummaryAsync(
            string promoterId,
            IDbTransaction? transaction = null)
        {
            var matches = Items.Where(item => item.PromoterId == promoterId).ToList();
            return Task.FromResult(new ProductEvaluationAggregate
            {
                TotalCount = matches.Count,
                HighQualityCount = matches.Sum(item => item.HighQuality),
                FastShippingCount = matches.Sum(item => item.FastShipping),
                GoodPackagingCount = matches.Sum(item => item.GoodPackaging),
                CostEffectiveCount = matches.Sum(item => item.CostEffective),
                AffordableCount = matches.Sum(item => item.Affordable),
                ReliablePromoterCount = matches.Sum(item => item.ReliablePromoter)
            });
        }
    }
}
