using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreshColdChain.Tests;

internal static class FreightAggregationScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("同供应商同温区合并重量只收一次首重包装", SamePackageAsync),
            ("不同供应商或温区分别计费", SeparatePackagesAsync),
            ("重复商品行与合并数量报价一致", DuplicateItemsAsync),
            ("包邮阈值及阶梯边界保持正确", ThresholdAndWeightAsync),
            ("地区与温区模板按明确优先级匹配", TemplatePriorityAsync),
            ("供应商缺失或模板无效时拒绝报价", InvalidInputAsync),
            ("B组适配保留供应商快照并支持同商品多供应商", AdapterPreservesSupplierAsync)
        };
        var failures = 0;
        foreach (var (name, run) in scenarios)
        {
            try { await run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception exception) { failures++; Console.WriteLine($"FAIL {name}\n{exception}"); }
        }
        Console.WriteLine($"运费聚合场景总数: {scenarios.Length}, 通过: {scenarios.Length - failures}, 失败: {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static InvProduct Product(string id, string supplier = "SUP1", string zone = "CHILLED", decimal weight = 1m) => new()
    {
        ProductID = id, ProductName = id, SupplierID = supplier, StorageReq = zone, WeightKG = weight, DefaultPrice = 50m
    };

    private static LogFreightTemplate Rule(string id = "RULE1", string zone = "*") => new()
    {
        TemplateID = id, TemplateName = id, TemperatureZone = zone,
        BaseWeight = 1m, BaseFee = 10m, ExtraWeightUnit = 1m, ExtraWeightFee = 2m, PackagingFee = 3m
    };

    private static FreightQuoteRequest Request(params FreightItemDto[] items) => new()
    {
        Province = "浙江省", City = "杭州市", District = "西湖区", GoodsAmount = 100m, Items = items.ToList()
    };

    private static ColdChainLogisticsService Service(List<InvProduct> products, params LogFreightTemplate[] rules) => new(
        FinancialProxy.Create<IProductRepository>((method, args) => method.Name == nameof(IProductRepository.GetByIdAsync)
            ? Task.FromResult(products.SingleOrDefault(product => product.ProductID == (string)args![0]!))
            : throw new NotSupportedException(method.Name)),
        FinancialProxy.Create<IStockSummaryRepository>(), FinancialProxy.Create<IStockBatchRepository>(),
        FinancialProxy.Create<ILogFreightTemplateRepository>((method, _) => method.Name == nameof(ILogFreightTemplateRepository.GetEnabledAsync)
            ? Task.FromResult(rules.ToList()) : throw new NotSupportedException(method.Name)),
        FinancialProxy.Create<ILogExpressDeliveryRepository>(), FinancialProxy.Create<ILogFulfillmentBatchItemRepository>(),
        new FinancialUnitOfWork(), NullLogger<ColdChainLogisticsService>.Instance);

    private static async Task<FreightQuoteDto> Quote(ColdChainLogisticsService service, FreightQuoteRequest request)
    {
        var result = await service.QuoteFreightAsync(request);
        AssertEx.True(result.IsSuccess && result.Data != null);
        return result.Data!;
    }

    private static async Task SamePackageAsync()
    {
        var service = Service([Product("P1"), Product("P2")], Rule());
        var quote = await Quote(service, Request(new() { ProductID = "P1", Quantity = 1 }, new() { ProductID = "P2", Quantity = 1 }));
        AssertEx.Equal(15m, quote.FreightAmount);
        AssertEx.Equal(2, quote.Items.Count);
        AssertEx.True(quote.RuleSummary.Contains("1 个计费包裹", StringComparison.Ordinal));
    }

    private static async Task SeparatePackagesAsync()
    {
        foreach (var differentSupplier in new[] { false, true })
        {
            var service = Service([Product("P1"), Product("P2", differentSupplier ? "SUP2" : "SUP1", differentSupplier ? "CHILLED" : "FROZEN")], Rule());
            var quote = await Quote(service, Request(new() { ProductID = "P1", Quantity = 1 }, new() { ProductID = "P2", Quantity = 1 }));
            AssertEx.Equal(26m, quote.FreightAmount);
        }
    }

    private static async Task DuplicateItemsAsync()
    {
        var service = Service([Product("P1")], Rule());
        var split = await Quote(service, Request(new() { ProductID = "P1", Quantity = 1 }, new() { ProductID = "P1", Quantity = 2 }));
        var merged = await Quote(service, Request(new FreightItemDto { ProductID = "P1", Quantity = 3 }));
        AssertEx.Equal(17m, split.FreightAmount);
        AssertEx.Equal(merged.FreightAmount, split.FreightAmount);
        AssertEx.Equal(1, split.Items.Count);
        AssertEx.Equal(3, split.Items.Single().Quantity);
    }

    private static async Task ThresholdAndWeightAsync()
    {
        var rule = Rule();
        rule.FreeShippingThreshold = 100m;
        var service = Service([Product("P1", weight: 1.01m)], rule);
        var request = Request(new FreightItemDto { ProductID = "P1", Quantity = 1 });
        AssertEx.Equal(0m, (await Quote(service, request)).FreightAmount);
        request.GoodsAmount = 99.99m;
        AssertEx.Equal(15m, (await Quote(service, request)).FreightAmount);
        var exact = Service([Product("P1", weight: 1m)], rule);
        AssertEx.Equal(13m, (await Quote(exact, request)).FreightAmount);
    }

    private static async Task TemplatePriorityAsync()
    {
        var wildcard = Rule("A-WILDCARD");
        var exactZone = Rule("Z-CHILLED", "CHILLED");
        exactZone.BaseFee = 20m;
        var district = Rule("DISTRICT");
        district.DestinationDistrict = "西湖区";
        district.BaseFee = 30m;
        var request = Request(new FreightItemDto { ProductID = "P1", Quantity = 1 });
        AssertEx.Equal(23m, (await Quote(Service([Product("P1")], wildcard, exactZone), request)).FreightAmount);
        AssertEx.Equal(33m, (await Quote(Service([Product("P1")], exactZone, wildcard, district), request)).FreightAmount);
        // 仓储返回顺序不应改变同优先级的选择。
        AssertEx.Equal(23m, (await Quote(Service([Product("P1")], exactZone, wildcard), request)).FreightAmount);
    }

    private static async Task InvalidInputAsync()
    {
        var request = Request(new FreightItemDto { ProductID = "P1", Quantity = 1 });
        AssertEx.True(!(await Service([Product("P1", supplier: "")], Rule()).QuoteFreightAsync(request)).IsSuccess);
        var invalidRule = Rule();
        invalidRule.ExtraWeightUnit = 0;
        AssertEx.True(!(await Service([Product("P1")], invalidRule).QuoteFreightAsync(request)).IsSuccess);
        AssertEx.True(!(await Service([Product("P1")]).QuoteFreightAsync(request)).IsSuccess);
        request.Items[0].Quantity = 0;
        AssertEx.True(!(await Service([Product("P1")], Rule()).QuoteFreightAsync(request)).IsSuccess);
    }

    private static async Task AdapterPreservesSupplierAsync()
    {
        var adapter = new GroupALogisticsServiceAdapter(new AttachedTransactionUnitOfWork(),
            Service([Product("P1", "CURRENT-SUPPLIER")], Rule()), FinancialProxy.Create<IGroupALogisticsExtensionProvider>());
        using var transaction = new FakeOrderTransaction();
        var quote = await adapter.QuoteFreightAsync(new FreightCalculationRequest
        {
            GoodsAmount = 100m,
            Items = [new() { ProductId = "P1", SupplierId = "ORDER-SUP1", Quantity = 1 },
                new() { ProductId = "P1", SupplierId = "ORDER-SUP2", Quantity = 1 }]
        }, transaction);
        AssertEx.Equal(26m, quote.FreightAmount);
        AssertEx.Equal("ORDER-SUP1", quote.Items[0].SupplierId);
        AssertEx.Equal("ORDER-SUP2", quote.Items[1].SupplierId);
        AssertEx.True(quote.Items.All(item => item.SubTotal == 50m));
    }
}
