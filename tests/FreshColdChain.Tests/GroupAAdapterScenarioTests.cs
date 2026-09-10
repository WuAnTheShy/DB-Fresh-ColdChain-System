using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Tests;

internal static class GroupAAdapterScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("库存适配器只通过 A 组服务返回可信快照", InventoryAdapterUsesServiceContractsAsync),
            ("物流适配器通过 A 组服务报价发货并查询状态", LogisticsAdapterUsesServiceContractsAsync),
            ("A 组尚未发货时返回待发货状态", MissingTraceReturnsPendingAsync),
            ("高级物流缺失时配置化兜底并识别温控异常", LogisticsFallbackTracksTemperatureExceptionAsync),
            ("兜底轨迹按事件编号保持幂等", LogisticsFallbackEventIsIdempotentAsync),
            ("物流更新后刷新不被基础发货状态覆盖", LogisticsStatusSurvivesRefreshAsync),
            ("已有发货单可恢复兜底轨迹并继续更新", ExistingShipmentCanResumeTrackingAsync),
            ("兜底物流超过预计时间后标记延误", LogisticsFallbackDetectsDelayAsync)
        };

        var failed = 0;
        foreach (var scenario in scenarios)
        {
            try
            {
                await scenario.Run();
                Console.WriteLine($"PASS {scenario.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"FAIL {scenario.Name}");
                Console.WriteLine(exception);
            }
        }

        Console.WriteLine(
            $"A 组适配场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task InventoryAdapterUsesServiceContractsAsync()
    {
        var unitOfWork = new AttachedTransactionUnitOfWork();
        var productService = new StubProductInventoryService
        {
            Product = new ProductDto
            {
                ProductID = "P1",
                ProductName = "车厘子",
                SupplierName = "测试供应商",
                DefaultPrice = 50m,
                Status = "ACTIVE"
            },
            Inventory = new InventoryDto
            {
                ProductID = "P1",
                AvailableQty = 3,
                TotalQty = 3
            }
        };
        var supplierService = new StubSupplierService();
        var adapter = new GroupAInventoryServiceAdapter(
            unitOfWork,
            productService,
            supplierService);
        using var transaction = new FakeOrderTransaction();

        var snapshots = await adapter.CheckAvailabilityAsync(
            [new InventoryAvailabilityItem { ProductId = "P1", SupplierId = "SUP1", Quantity = 2 }],
            transaction);

        AssertEx.True(ReferenceEquals(transaction, unitOfWork.AttachedTransaction));
        AssertEx.Equal(1, snapshots.Count);
        AssertEx.Equal("P1", snapshots[0].ProductId);
        AssertEx.Equal("SUP1", snapshots[0].SupplierId);
        AssertEx.Equal(50m, snapshots[0].UnitPrice);

        productService.Inventory.AvailableQty = 1;
        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            adapter.CheckAvailabilityAsync(
                [new InventoryAvailabilityItem { ProductId = "P1", SupplierId = "SUP1", Quantity = 2 }],
                transaction));

        // 供应商被禁用或审核驳回时同样不可下单，由 A 组公开的供应商服务判定
        productService.Inventory.AvailableQty = 3;
        supplierService.Status = "Disabled";
        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            adapter.CheckAvailabilityAsync(
                [new InventoryAvailabilityItem { ProductId = "P1", SupplierId = "SUP1", Quantity = 2 }],
                transaction));

        supplierService.Status = "Active";
        var recovered = await adapter.CheckAvailabilityAsync(
            [new InventoryAvailabilityItem { ProductId = "P1", SupplierId = "SUP1", Quantity = 2 }],
            transaction);
        AssertEx.Equal(1, recovered.Count);
    }

    private static async Task LogisticsAdapterUsesServiceContractsAsync()
    {
        var unitOfWork = new AttachedTransactionUnitOfWork();
        var coldChainService = new StubColdChainLogisticsService
        {
            QuoteResponse = ApiResponse<FreightQuoteDto>.Success(new FreightQuoteDto
            {
                FreightAmount = 12.5m
            }),
            TraceResponse = ApiResponse<List<DeliveryTraceDto>>.Success(
            [
                new DeliveryTraceDto
                {
                    DeliveryID = "DEL1",
                    OrderID = "ORDER1",
                    SupplierID = "SUP1",
                    TrackingNo = "TRACK1",
                    LogisticsStatus = "SHIPPED",
                    ShippedAt = DateTime.Now
                }
            ])
        };
        var adapter = new GroupALogisticsServiceAdapter(
            unitOfWork,
            coldChainService,
            CreateExtensionProvider());
        using var transaction = new FakeOrderTransaction();
        var items = new List<FulfillmentOrderItem>
        {
            new()
            {
                ProductId = "P1", ProductName = "车厘子", SupplierId = "SUP1",
                Quantity = 2, UnitPrice = 50m, SubTotal = 100m
            }
        };

        var freight = await adapter.CalculateFreightAsync(
            new FreightCalculationRequest
            {
                Province = "浙江省",
                City = "杭州市",
                District = "西湖区",
                GoodsAmount = 100m,
                Items = items
            },
            transaction);
        await adapter.CreateShipmentAsync(
            new FulfillmentOrderRequest { OrderId = "ORDER1", Items = items },
            transaction);
        var statuses = await adapter.GetSupplierStatusesAsync("ORDER1", ["SUP1"]);

        AssertEx.Equal(12.5m, freight);
        AssertEx.Equal(1, coldChainService.ShipmentRequests.Count);
        AssertEx.Equal("SUP1", coldChainService.ShipmentRequests[0].SupplierID);
        AssertEx.Equal("SHIPPED", statuses[0].StatusName);
        AssertEx.Equal("TRACK1", statuses[0].TrackingNo);
        AssertEx.True(ReferenceEquals(transaction, unitOfWork.AttachedTransaction));
    }

    private static async Task MissingTraceReturnsPendingAsync()
    {
        var adapter = new GroupALogisticsServiceAdapter(
            new AttachedTransactionUnitOfWork(),
            new StubColdChainLogisticsService
            {
                TraceResponse = ApiResponse<List<DeliveryTraceDto>>.Fail("未找到", 404)
            },
            CreateExtensionProvider());

        var statuses = await adapter.GetSupplierStatusesAsync("ORDER1", ["SUP2", "SUP1", "SUP2"]);

        AssertEx.Equal(2, statuses.Count);
        AssertEx.True(statuses.All(status => status.StatusName == "待发货"));
    }

    private static async Task LogisticsFallbackTracksTemperatureExceptionAsync()
    {
        var provider = CreateExtensionProvider();
        var adapter = new GroupALogisticsServiceAdapter(
            new AttachedTransactionUnitOfWork(),
            new StubColdChainLogisticsService(),
            provider);
        using var transaction = new FakeOrderTransaction();
        var order = new FulfillmentOrderRequest
        {
            OrderId = "ORDER1",
            Items =
            [
                new FulfillmentOrderItem
                {
                    ProductId = "P1",
                    ProductName = "车厘子",
                    SupplierId = "SUP1",
                    Quantity = 1,
                    UnitPrice = 50m,
                    SubTotal = 50m
                }
            ]
        };

        var shipment = await adapter.CreateSupplierShipmentAsync(
            order,
            new SupplierShipmentCommand
            {
                SupplierId = "SUP1",
                CarrierCode = "CUSTOM",
                CarrierName = "自定义承运商",
                TrackingNo = "CUSTOM-TRACK",
                PackageTemperature = "CHILLED"
            },
            transaction);
        var exception = await adapter.AppendTrackingEventAsync(
            new LogisticsTrackingEventCommand
            {
                OrderId = "ORDER1",
                SupplierId = "SUP1",
                StatusCode = LogisticsStatusCodes.InTransit,
                Location = "杭州中转场",
                Description = "运输温度采集",
                TemperatureCelsius = 12m,
                OccurredAt = DateTime.Now.AddHours(1)
            },
            transaction);

        AssertEx.Equal("自定义承运商", shipment.CarrierName);
        AssertEx.Equal("CUSTOM-TRACK", shipment.TrackingNo);
        AssertEx.True(shipment.IsFallback);
        AssertEx.Equal(1, shipment.Events.Count);
        AssertEx.Equal(LogisticsStatusCodes.Exception, exception.StatusCode);
        AssertEx.True(exception.HasException);
        AssertEx.True(exception.Events.Last().IsTemperatureException);
    }

    private static async Task LogisticsFallbackDetectsDelayAsync()
    {
        var provider = CreateExtensionProvider();
        using var transaction = new FakeOrderTransaction();
        await provider.RegisterShipmentAsync(
            new LogisticsShipmentRegistration
            {
                DeliveryId = "DEL1",
                OrderId = "ORDER1",
                SupplierId = "SUP1",
                BaseTrackingNo = "TRACK1",
                BaseStatus = LogisticsStatusCodes.Shipped,
                ShippedAt = DateTime.Now.AddDays(-2),
                Command = new SupplierShipmentCommand
                {
                    SupplierId = "SUP1",
                    PackageTemperature = "CHILLED",
                    EstimatedArrivalAt = DateTime.Now.AddMinutes(-1)
                }
            },
            transaction);

        var result = await provider.GetSnapshotAsync(new LogisticsTraceSeed
        {
            DeliveryId = "DEL1",
            OrderId = "ORDER1",
            SupplierId = "SUP1",
            TrackingNo = "TRACK1",
            StatusCode = LogisticsStatusCodes.Shipped,
            ShippedAt = DateTime.Now.AddDays(-2)
        });

        AssertEx.Equal(LogisticsStatusCodes.Exception, result.StatusCode);
        AssertEx.True(result.HasException);
        AssertEx.Equal("测试包裹已延误", result.ExceptionMessage);
    }

    private static async Task LogisticsFallbackEventIsIdempotentAsync()
    {
        var provider = CreateExtensionProvider();
        using var transaction = new FakeOrderTransaction();
        await provider.RegisterShipmentAsync(
            new LogisticsShipmentRegistration
            {
                DeliveryId = "DEL-IDEMPOTENT",
                OrderId = "ORDER-IDEMPOTENT",
                SupplierId = "SUP1",
                BaseTrackingNo = "TRACK-IDEMPOTENT",
                BaseStatus = LogisticsStatusCodes.Shipped,
                ShippedAt = DateTime.Now.AddHours(-1),
                Command = new SupplierShipmentCommand
                {
                    SupplierId = "SUP1",
                    PackageTemperature = "CHILLED"
                }
            },
            transaction);
        var command = new LogisticsTrackingEventCommand
        {
            EventId = "EVENT-IDEMPOTENT",
            OrderId = "ORDER-IDEMPOTENT",
            SupplierId = "SUP1",
            StatusCode = LogisticsStatusCodes.InTransit,
            Location = "杭州中转场",
            Description = "包裹运输中",
            OccurredAt = DateTime.Now,
            TemperatureCelsius = 4m
        };

        var first = await provider.AppendTrackingEventAsync(command, transaction);
        var second = await provider.AppendTrackingEventAsync(command, transaction);

        AssertEx.Equal(first.Events.Count, second.Events.Count);
        AssertEx.Equal(1, second.Events.Count(item => item.EventId == command.EventId));
    }

    private static async Task LogisticsStatusSurvivesRefreshAsync()
    {
        var provider = CreateExtensionProvider();
        using var transaction = new FakeOrderTransaction();
        var registration = new LogisticsShipmentRegistration
        {
            DeliveryId = "DEL-REFRESH", OrderId = "ORDER-REFRESH", SupplierId = "SUP1",
            BaseTrackingNo = "TRACK-REFRESH", ShippedAt = DateTime.Now,
            Command = new SupplierShipmentCommand { SupplierId = "SUP1" }
        };
        await provider.RegisterShipmentAsync(registration, transaction);
        foreach (var status in new[] { LogisticsStatusCodes.InTransit, LogisticsStatusCodes.Exception,
            LogisticsStatusCodes.InTransit, LogisticsStatusCodes.OutForDelivery, LogisticsStatusCodes.Delivered })
        {
            var updated = await provider.AppendTrackingEventAsync(new LogisticsTrackingEventCommand
            {
                OrderId = registration.OrderId, SupplierId = registration.SupplierId,
                StatusCode = status, Description = "回归轨迹", OccurredAt = DateTime.Now
            }, transaction);
            var refreshed = await provider.GetSnapshotAsync(new LogisticsTraceSeed
            {
                OrderId = registration.OrderId, SupplierId = registration.SupplierId,
                DeliveryId = registration.DeliveryId, StatusCode = LogisticsStatusCodes.Shipped,
                ShippedAt = registration.ShippedAt
            });
            AssertEx.Equal(status, refreshed.StatusCode);
            AssertEx.Equal(updated.Events.Count, refreshed.Events.Count);
        }
    }

    private static async Task ExistingShipmentCanResumeTrackingAsync()
    {
        var provider = CreateExtensionProvider();
        using var transaction = new FakeOrderTransaction();
        var seed = new LogisticsTraceSeed
        {
            OrderId = "ORDER-RESTART", SupplierId = "SUP1", DeliveryId = "DEL-RESTART",
            StatusCode = LogisticsStatusCodes.Shipped, ShippedAt = DateTime.Now
        };
        var first = await provider.GetSnapshotAsync(seed);
        var second = await provider.GetSnapshotAsync(seed);
        AssertEx.Equal(first.Events.Single().EventId, second.Events.Single().EventId);
        var updated = await provider.AppendTrackingEventAsync(new LogisticsTrackingEventCommand
        {
            OrderId = seed.OrderId, SupplierId = seed.SupplierId, StatusCode = LogisticsStatusCodes.InTransit,
            Description = "重启后继续配送", OccurredAt = DateTime.Now
        }, transaction);
        AssertEx.Equal(2, updated.Events.Count);
    }

    private static FallbackGroupALogisticsExtensionProvider CreateExtensionProvider() =>
        new(Options.Create(new GroupALogisticsFallbackOptions
        {
            CarrierCode = "TEST_CARRIER",
            CarrierName = "测试承运商",
            OriginLocation = "测试冷链仓",
            ShippedDescription = "测试包裹已出库",
            DelayDescription = "测试包裹已延误",
            EstimatedTransitHours = 24,
            ChilledMinimumCelsius = 0,
            ChilledMaximumCelsius = 8,
            FrozenMaximumCelsius = -18
        }));
}

internal sealed class AttachedTransactionUnitOfWork : IUnitOfWork
{
    public IDbTransaction? AttachedTransaction { get; private set; }
    public IDbConnection Connection => AttachedTransaction?.Connection ?? throw new InvalidOperationException();
    public IDbTransaction? Transaction => AttachedTransaction;

    public void AttachExternalTransaction(IDbTransaction externalTransaction) =>
        AttachedTransaction = externalTransaction;

    public Task BeginAsync() => Task.CompletedTask;
    public Task CommitAsync() => Task.CompletedTask;
    public Task RollbackAsync() => Task.CompletedTask;
    public void Dispose() { }
}

internal sealed class StubProductInventoryService : IProductInventoryService
{
    public required ProductDto Product { get; init; }
    public required InventoryDto Inventory { get; init; }

    public Task<ApiResponse<ProductDto>> GetProductByIdAsync(string id) =>
        Task.FromResult(id == Product.ProductID
            ? ApiResponse<ProductDto>.Success(Product)
            : ApiResponse<ProductDto>.Fail("不存在", 404));

    public Task<ApiResponse<InventoryDto>> GetInventoryAsync(string productId) =>
        Task.FromResult(productId == Inventory.ProductID
            ? ApiResponse<InventoryDto>.Success(Inventory)
            : ApiResponse<InventoryDto>.Fail("不存在", 404));

    public Task<ApiResponse<SupplierGoodsInventoryDto>> GetSupplierGoodsInventoryAsync(
        string productId,
        string supplierId) =>
        Task.FromResult(productId == Product.ProductID && supplierId == "SUP1"
            ? ApiResponse<SupplierGoodsInventoryDto>.Success(new SupplierGoodsInventoryDto
            {
                ProductID = Product.ProductID,
                ProductName = Product.ProductName,
                SupplierID = supplierId,
                SalePrice = Product.DefaultPrice,
                Status = "ACTIVE",
                AvailableQty = Inventory.AvailableQty
            })
            : ApiResponse<SupplierGoodsInventoryDto>.Fail("该供应商未对此商品建立货物", 404));

    public Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(int pageIndex, int pageSize, string? keyword = null) => throw new NotSupportedException();
    public Task<ApiResponse<ProductSupplierMediaDto>> GetSupplierProductMediaAsync(string productId, string? supplierId) => throw new NotSupportedException();
    public Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto) => throw new NotSupportedException();
    public Task<ApiResponse<ProductDto>> UpdateProductAsync(string id, UpdateProductDto dto) => throw new NotSupportedException();
    public Task<ApiResponse> DeleteProductAsync(string id) => throw new NotSupportedException();
    public Task<ApiResponse> AddProductImagesAsync(string productId, IReadOnlyList<ProductImageUploadDto> images) => throw new NotSupportedException();
    public Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync() => throw new NotSupportedException();
    public Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto) => throw new NotSupportedException();
    public Task<ApiResponse<int>> GetProductStockAsync(string productId) => throw new NotSupportedException();
    public Task<ApiResponse<List<InventoryDto>>> GetLowStockProductsAsync(int threshold = 10) => throw new NotSupportedException();
    public Task<ApiResponse> StockInAsync(UpdateInventoryDto dto, string? supplierId = null, string? batchNo = null, DateTime? productionDate = null) => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierQuoteOptionDto>>> GetStockInSupplierOptionsAsync(string productId) => throw new NotSupportedException();
    public Task<ApiResponse> StockOutAsync(UpdateInventoryDto dto) => throw new NotSupportedException();
    public Task<ApiResponse<List<StockBatchDto>>> GetBatchesAsync(string productId) => throw new NotSupportedException();
    public Task<ApiResponse<StockBatchDto>> AddBatchAsync(CreateStockBatchDto dto) => throw new NotSupportedException();
    public Task MarkExpiredBatchesAsync() => throw new NotSupportedException();
}

internal sealed class StubSupplierService : ISupplierService
{
    public string SupplierId { get; init; } = "SUP1";
    public string SupplierName { get; init; } = "测试供应商";
    public string Status { get; set; } = "Active";

    public Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(string id) =>
        Task.FromResult(string.Equals(id, SupplierId, StringComparison.Ordinal)
            ? ApiResponse<SupplierDto>.Success(new SupplierDto
            {
                SupplierID = SupplierId,
                SupplierName = SupplierName,
                Status = Status
            })
            : ApiResponse<SupplierDto>.Fail("供应商不存在", 404));

    public Task<ApiResponse<PagedResult<SupplierDto>>> GetSuppliersAsync(int pageIndex, int pageSize) => throw new NotSupportedException();
    public Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto) => throw new NotSupportedException();
    public Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(string id, CreateSupplierDto dto) => throw new NotSupportedException();
    public Task<ApiResponse> DeleteSupplierAsync(string id) => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierProductQuoteDto>>> GetSupplierProductQuotesAsync(string supplierId) => throw new NotSupportedException();
    public Task<ApiResponse> AddProductImageAsync(string supplierId, string productId, byte[] imageData, string imageType) => throw new NotSupportedException();
    public Task<ApiResponse<ProductImageContentDto>> GetProductImageContentAsync(string imageId) => throw new NotSupportedException();
    public Task<ApiResponse<string>> DeleteProductImageAsync(string supplierId, string imageId) => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierAccountDto>>> FindSupplierAccountAsync(string? supplierId = null, string? supplierName = null, string? loginAccount = null, string? contactPhone = null) => throw new NotSupportedException();
    public Task<ApiResponse<bool>> VerifySupplierPasswordAsync(string loginAccount, string password) => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierProductEntryDto>>> SearchSupplierProductEntriesAsync(string? keyword) => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierDto>>> GetAllSuppliersAsync() => throw new NotSupportedException();
    public Task<ApiResponse<List<SupplierDto>>> GetSuppliersByStatusAsync(string status) => throw new NotSupportedException();
    public Task<ApiResponse> SetSupplierStatusAsync(string supplierId, string targetStatus, int? creditLevel = null) => throw new NotSupportedException();
}

internal sealed class StubColdChainLogisticsService : IColdChainLogisticsService
{
    public ApiResponse<FreightQuoteDto> QuoteResponse { get; init; } =
        ApiResponse<FreightQuoteDto>.Success(new FreightQuoteDto());
    public ApiResponse<List<DeliveryTraceDto>> TraceResponse { get; init; } =
        ApiResponse<List<DeliveryTraceDto>>.Fail("未找到", 404);
    public List<ShipmentRequest> ShipmentRequests { get; } = [];

    public Task<ApiResponse<FreightQuoteDto>> QuoteFreightAsync(FreightQuoteRequest request) =>
        Task.FromResult(QuoteResponse);

    public Task<ApiResponse<LogExpressDelivery>> CreateShipmentAsync(ShipmentRequest request)
    {
        ShipmentRequests.Add(request);
        return Task.FromResult(ApiResponse<LogExpressDelivery>.Success(new LogExpressDelivery
        {
            OrderID = request.OrderID,
            SupplierID = request.SupplierID,
            TrackingNo = "TRACK1"
        }));
    }

    public Task<ApiResponse<List<DeliveryTraceDto>>> GetTraceabilityByOrderAsync(string orderId) =>
        Task.FromResult(TraceResponse);

    public Task<ApiResponse<DeliveryTraceDto>> GetTraceabilityByDeliveryAsync(string deliveryId) => throw new NotSupportedException();
    public Task<ApiResponse<BatchTraceDto>> GetBatchTraceAsync(string batchId) => throw new NotSupportedException();
}
