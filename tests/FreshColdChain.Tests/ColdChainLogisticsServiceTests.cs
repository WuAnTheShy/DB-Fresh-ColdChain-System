using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Moq;
using Xunit;

namespace FreshColdChain.Tests;

public class ColdChainLogisticsServiceTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockSummaryRepository> _stockSummary = new();
    private readonly Mock<IStockBatchRepository> _batches = new();
    private readonly Mock<ILogFreightTemplateRepository> _templates = new();
    private readonly Mock<ILogExpressDeliveryRepository> _deliveries = new();
    private readonly Mock<ILogFulfillmentBatchItemRepository> _allocations = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ColdChainLogisticsService Sut() => new(
        _products.Object, _stockSummary.Object, _batches.Object,
        _templates.Object, _deliveries.Object, _allocations.Object, _uow.Object);

    // ==================== 运费报价 ====================

    [Fact]
    public async Task QuoteFreight_SingleItem_CalculatesTieredFreight()
    {
        var product = new InvProduct { ProductID = "P1", WeightKG = 5m, StorageReq = "CHILLED" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(product);

        var rule = new LogFreightTemplate
        {
            TemperatureZone = "CHILLED", BaseWeight = 1m, BaseFee = 10m,
            ExtraWeightUnit = 1m, ExtraWeightFee = 5m, PackagingFee = 3m,
            DestinationProvince = "广东", DestinationCity = "*", DestinationDistrict = "*",
            IsEnabled = 1
        };
        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate> { rule });

        var request = new FreightQuoteRequest
        {
            Province = "广东", City = "深圳", District = "南山", GoodsAmount = 100,
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 3 } }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.True(result.IsSuccess);
        // weight = 5 * 3 = 15kg, extra units = ceil((15-1)/1) = 14
        // fee = 10 + 14*5 + 3 = 83
        Assert.Equal(83m, result.Data!.FreightAmount);
    }

    [Fact]
    public async Task QuoteFreight_FreeShippingThreshold_ReturnsZero()
    {
        var product = new InvProduct { ProductID = "P1", WeightKG = 3m, StorageReq = "CHILLED" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(product);

        var rule = new LogFreightTemplate
        {
            TemperatureZone = "CHILLED", BaseWeight = 1m, BaseFee = 10m,
            ExtraWeightUnit = 1m, ExtraWeightFee = 5m, PackagingFee = 3m,
            FreeShippingThreshold = 80m,
            DestinationProvince = "*", DestinationCity = "*", DestinationDistrict = "*",
            IsEnabled = 1
        };
        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate> { rule });

        var request = new FreightQuoteRequest
        {
            Province = "广东", City = "深圳", District = "南山", GoodsAmount = 100, // >= 80
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 1 } }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Data!.FreightAmount); // 免运费
    }

    [Fact]
    public async Task QuoteFreight_RegionPriority_PicksMostSpecific()
    {
        var product = new InvProduct { ProductID = "P1", WeightKG = 2m, StorageReq = "CHILLED" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(product);

        var ruleProvince = new LogFreightTemplate
        {
            TemplateID = "R1", TemplateName = "广东省", TemperatureZone = "CHILLED",
            BaseWeight = 1m, BaseFee = 8m, ExtraWeightUnit = 1m, ExtraWeightFee = 2m, PackagingFee = 0,
            DestinationProvince = "广东", DestinationCity = "*", DestinationDistrict = "*", IsEnabled = 1
        };
        var ruleCity = new LogFreightTemplate
        {
            TemplateID = "R2", TemplateName = "深圳市", TemperatureZone = "CHILLED",
            BaseWeight = 1m, BaseFee = 5m, ExtraWeightUnit = 1m, ExtraWeightFee = 1m, PackagingFee = 0,
            DestinationProvince = "广东", DestinationCity = "深圳", DestinationDistrict = "*", IsEnabled = 1
        };
        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate> { ruleProvince, ruleCity });

        var request = new FreightQuoteRequest
        {
            Province = "广东", City = "深圳", District = "南山", GoodsAmount = 50,
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 1 } }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.True(result.IsSuccess);
        // 应匹配深圳市（city=2分 vs province=1分）：weight=2, extra=ceil((2-1)/1)=1, fee=5+1*1+0=6
        Assert.Equal(6m, result.Data!.FreightAmount);
    }

    [Fact]
    public async Task QuoteFreight_ExtraWeightUnitZero_DoesNotCrash()
    {
        var product = new InvProduct { ProductID = "P1", WeightKG = 10m, StorageReq = "CHILLED" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(product);

        var rule = new LogFreightTemplate
        {
            TemperatureZone = "CHILLED", BaseWeight = 1m, BaseFee = 10m,
            ExtraWeightUnit = 0m, ExtraWeightFee = 5m, PackagingFee = 3m, // 除零！
            DestinationProvince = "*", DestinationCity = "*", DestinationDistrict = "*", IsEnabled = 1
        };
        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate> { rule });

        var request = new FreightQuoteRequest
        {
            Province = "广东", City = "深圳", District = "南山", GoodsAmount = 50,
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 1 } }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.True(result.IsSuccess);
        // ExtraWeightUnit=0 → extraUnits=0, fee = 10 + 0 + 3 = 13
        Assert.Equal(13m, result.Data!.FreightAmount);
    }

    [Fact]
    public async Task QuoteFreight_NoMatchingRule_ReturnsError()
    {
        var product = new InvProduct { ProductID = "P1", WeightKG = 1m, StorageReq = "FROZEN" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(product);

        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate>()); // 没有冷冻模板

        var request = new FreightQuoteRequest
        {
            Province = "广东", City = "深圳", District = "南山", GoodsAmount = 50,
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 1 } }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("FROZEN", result.Message);
    }

    [Fact]
    public async Task QuoteFreight_MultipleItems_AggregatesTotal()
    {
        var p1 = new InvProduct { ProductID = "P1", WeightKG = 2m, StorageReq = "CHILLED" };
        var p2 = new InvProduct { ProductID = "P2", WeightKG = 3m, StorageReq = "CHILLED" };
        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(p1);
        _products.Setup(x => x.GetByIdAsync("P2")).ReturnsAsync(p2);

        var rule = new LogFreightTemplate
        {
            TemperatureZone = "CHILLED", BaseWeight = 1m, BaseFee = 10m,
            ExtraWeightUnit = 1m, ExtraWeightFee = 2m, PackagingFee = 0,
            DestinationProvince = "*", DestinationCity = "*", DestinationDistrict = "*", IsEnabled = 1
        };
        _templates.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new List<LogFreightTemplate> { rule });

        var request = new FreightQuoteRequest
        {
            Province = "北京", City = "北京", District = "朝阳", GoodsAmount = 50,
            Items = new List<FreightItemDto>
            {
                new() { ProductID = "P1", Quantity = 2 },
                new() { ProductID = "P2", Quantity = 1 }
            }
        };

        var result = await Sut().QuoteFreightAsync(request);

        Assert.True(result.IsSuccess);
        // P1: weight=4, extra=ceil((4-1)/1)=3, fee=10+3*2+0=16
        // P2: weight=3, extra=ceil((3-1)/1)=2, fee=10+2*2+0=14
        // total = 30
        Assert.Equal(30m, result.Data!.FreightAmount);
    }

    // ==================== 冷链发货 ====================

    [Fact]
    public async Task CreateShipment_FefoDeduction_SuccessWithTraceability()
    {
        var stock = new InvStockSummary
        {
            StockID = "S1", ProductID = "P1", TotalQty = 100, LockedQty = 10, AvailableQty = 90
        };
        _stockSummary.Setup(x => x.GetByProductIdForUpdateAsync("P1")).ReturnsAsync(stock);

        var batches = new List<InvStockBatch>
        {
            new() { BatchID = "B1", ProductID = "P1", BatchNo = "BN001", ExpiryDate = DateTime.Today.AddDays(5), CurrentQty = 3, Status = "ACTIVE" },
            new() { BatchID = "B2", ProductID = "P1", BatchNo = "BN002", ExpiryDate = DateTime.Today.AddDays(10), CurrentQty = 50, Status = "ACTIVE" }
        };
        _batches.Setup(x => x.GetByProductIdAsync("P1")).ReturnsAsync(batches);

        var request = new ShipmentRequest
        {
            OrderID = "O1", SupplierID = "SUP1",
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 5 } }
        };

        var result = await Sut().CreateShipmentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("O1", result.Data!.OrderID);
        Assert.Equal("SUP1", result.Data.SupplierID);
        Assert.StartsWith("CC", result.Data.TrackingNo);

        // 验证 FEFO：B1(expiry更早)先被扣完 3, 再从 B2 扣 2
        Assert.Equal(0, batches[0].CurrentQty);
        Assert.Equal("DEPLETED", batches[0].Status);
        Assert.Equal(48, batches[1].CurrentQty);

        // 验证库存汇总：TotalQty 95, LockedQty 5 (10-5), Available 90
        Assert.Equal(95, stock.TotalQty);
        Assert.Equal(5, stock.LockedQty);
        Assert.Equal(90, stock.AvailableQty);

        // 验证溯源写入 2 条
        _allocations.Verify(x => x.AddAsync(It.Is<LogFulfillmentBatchItem>(a => a.BatchID == "B1" && a.Quantity == 3)), Times.Once);
        _allocations.Verify(x => x.AddAsync(It.Is<LogFulfillmentBatchItem>(a => a.BatchID == "B2" && a.Quantity == 2)), Times.Once);

        // 验证事务
        _uow.Verify(x => x.BeginAsync(), Times.Once);
        _uow.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateShipment_InsufficientStock_ReturnsErrorAndRollsBack()
    {
        var stock = new InvStockSummary
        {
            StockID = "S1", ProductID = "P1", TotalQty = 10, LockedQty = 0, AvailableQty = 10
        };
        _stockSummary.Setup(x => x.GetByProductIdForUpdateAsync("P1")).ReturnsAsync(stock);

        var request = new ShipmentRequest
        {
            OrderID = "O1", SupplierID = "SUP1",
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 100 } }
        };

        var result = await Sut().CreateShipmentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("库存不足", result.Message);
        _uow.Verify(x => x.RollbackAsync(), Times.Once);
        _uow.Verify(x => x.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateShipment_InsufficientBatchStock_RollsBack()
    {
        var stock = new InvStockSummary
        {
            StockID = "S1", ProductID = "P1", TotalQty = 5, LockedQty = 0, AvailableQty = 5
        };
        _stockSummary.Setup(x => x.GetByProductIdForUpdateAsync("P1")).ReturnsAsync(stock);

        // 汇总够，但批次不够（只有 2）
        var batches = new List<InvStockBatch>
        {
            new() { BatchID = "B1", ProductID = "P1", BatchNo = "BN001", ExpiryDate = DateTime.Today, CurrentQty = 2, Status = "ACTIVE" }
        };
        _batches.Setup(x => x.GetByProductIdAsync("P1")).ReturnsAsync(batches);

        var request = new ShipmentRequest
        {
            OrderID = "O1", SupplierID = "SUP1",
            Items = new List<FreightItemDto> { new() { ProductID = "P1", Quantity = 5 } }
        };

        var result = await Sut().CreateShipmentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("批次库存不足", result.Message);
        _uow.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateShipment_MissingParameters_ReturnsError()
    {
        var result = await Sut().CreateShipmentAsync(new ShipmentRequest
        {
            OrderID = "", SupplierID = "", Items = new List<FreightItemDto>()
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("不完整", result.Message);
    }

    // ==================== 精准溯源 ====================

    [Fact]
    public async Task GetTraceabilityByOrder_Found_ReturnsTrace()
    {
        var delivery = new LogExpressDelivery
        {
            DeliveryID = "D1", OrderID = "O1", SupplierID = "SUP1",
            TrackingNo = "CC123", LogisticsStatus = "SHIPPED", ShippedAt = DateTime.Today
        };
        _deliveries.Setup(x => x.GetByOrderIdAsync("O1")).ReturnsAsync(new List<LogExpressDelivery> { delivery });

        var allocs = new List<LogFulfillmentBatchItem>
        {
            new() { AllocationID = "A1", DeliveryID = "D1", ProductID = "P1", BatchID = "B1", Quantity = 3 }
        };
        _allocations.Setup(x => x.GetByDeliveryIdAsync("D1")).ReturnsAsync(allocs);

        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(new InvProduct { ProductID = "P1", ProductName = "三文鱼" });
        _batches.Setup(x => x.GetByIdAsync("B1")).ReturnsAsync(new InvStockBatch { BatchID = "B1", BatchNo = "BN001", ExpiryDate = DateTime.Today.AddDays(3) });

        var result = await Sut().GetTraceabilityByOrderAsync("O1");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("D1", result.Data![0].DeliveryID);
        Assert.Single(result.Data[0].Allocations);
        Assert.Equal("三文鱼", result.Data[0].Allocations[0].ProductName);
        Assert.Equal("BN001", result.Data[0].Allocations[0].BatchNo);
    }

    [Fact]
    public async Task GetTraceabilityByOrder_NotFound_Returns404()
    {
        _deliveries.Setup(x => x.GetByOrderIdAsync("O99")).ReturnsAsync(new List<LogExpressDelivery>());

        var result = await Sut().GetTraceabilityByOrderAsync("O99");

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Code);
    }

    [Fact]
    public async Task GetBatchTrace_ReverseTrace_IncludesDeliveryInfo()
    {
        var batch = new InvStockBatch
        {
            BatchID = "B1", ProductID = "P1", BatchNo = "BN001", ExpiryDate = DateTime.Today.AddDays(30)
        };
        _batches.Setup(x => x.GetByIdAsync("B1")).ReturnsAsync(batch);

        var allocs = new List<LogFulfillmentBatchItem>
        {
            new() { AllocationID = "A1", DeliveryID = "D1", ProductID = "P1", BatchID = "B1", Quantity = 2 },
            new() { AllocationID = "A2", DeliveryID = "D2", ProductID = "P1", BatchID = "B1", Quantity = 3 }
        };
        _allocations.Setup(x => x.GetByBatchIdAsync("B1")).ReturnsAsync(allocs);

        _deliveries.Setup(x => x.GetByIdAsync("D1")).ReturnsAsync(new LogExpressDelivery
            { DeliveryID = "D1", TrackingNo = "CC001" });
        _deliveries.Setup(x => x.GetByIdAsync("D2")).ReturnsAsync(new LogExpressDelivery
            { DeliveryID = "D2", TrackingNo = "CC002" });

        _products.Setup(x => x.GetByIdAsync("P1")).ReturnsAsync(new InvProduct { ProductID = "P1", ProductName = "金枪鱼" });

        var result = await Sut().GetBatchTraceAsync("B1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Allocations.Count);
        Assert.Equal("D1", result.Data.Allocations[0].DeliveryID);
        Assert.Equal("CC001", result.Data.Allocations[0].TrackingNo);
        Assert.Equal("D2", result.Data.Allocations[1].DeliveryID);
        Assert.Equal("CC002", result.Data.Allocations[1].TrackingNo);
        Assert.Equal("金枪鱼", result.Data.ProductName);
    }

    [Fact]
    public async Task GetBatchTrace_NotFound_Returns404()
    {
        _batches.Setup(x => x.GetByIdAsync("B99")).ReturnsAsync((InvStockBatch?)null);

        var result = await Sut().GetBatchTraceAsync("B99");

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Code);
    }
}
