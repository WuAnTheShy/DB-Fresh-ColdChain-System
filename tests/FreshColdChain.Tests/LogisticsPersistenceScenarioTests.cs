using System.Data;
using System.Text.Json;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Tests;

internal static class LogisticsPersistenceScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("物流登记参加外部事务且服务重建后可读取", RegistrationAsync),
            ("物流登记回滚不留下元数据或事件", RegistrationRollbackAsync),
            ("轨迹提交后刷新不依赖旧基础快照", AppendAsync),
            ("轨迹末步失败时事件元数据和状态一起回滚", AppendRollbackAsync),
            ("事件重试验证内容并允许状态推进后的原样重放", IdempotencyAsync),
            ("事件不能跨供应商复用且发货归属必须一致", OwnershipAsync),
            ("温控异常阻止签收且恢复后保留异常历史", TemperatureAsync),
            ("延误读取稳定无写入并在下次事件事务中落库", DelayAsync),
            ("旧发货单可继续跟踪且不虚构历史签收时间", LegacyAsync),
            ("物流拒绝逆序未来事件和非法状态", ValidationAsync),
            ("默认选择Oracle且非开发环境禁止内存物流", ProviderSelectionAsync),
            ("B组重复事件仍交由A组验证原载荷", BRetryAsync)
        };
        var failed = 0;
        foreach (var (name, run) in scenarios)
        {
            try { await run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception exception) { failed++; Console.WriteLine($"FAIL {name}\n{exception}"); }
        }
        Console.WriteLine($"物流持久化场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static readonly DateTime ShippedAt = DateTime.Now.AddHours(-4);
    private static OracleGroupALogisticsExtensionProvider Service(LogisticsStore store) =>
        new(new TransactionalLogisticsRepository(store), Options.Create(new GroupALogisticsOptions()));
    private static LogisticsTraceSeed Seed(string supplier = "SUP1") => new()
        { OrderId = "ORDER1", SupplierId = supplier, StatusCode = LogisticsStatusCodes.Shipped };
    private static LogisticsShipmentRegistration Registration(DateTime? eta = null, string deliveryId = "DEL1") => new()
    {
        OrderId = "ORDER1", SupplierId = "SUP1", DeliveryId = deliveryId,
        Command = new() { SupplierId = "SUP1", CarrierCode = "TEST", CarrierName = "测试承运商",
            TrackingNo = "TRACK-EXTERNAL", EstimatedArrivalAt = eta }
    };
    private static LogisticsTrackingEventCommand Event(string id = "EVENT1", string status = LogisticsStatusCodes.InTransit,
        int minutes = 1, decimal? temperature = 4m, string supplier = "SUP1", string description = "到达冷链站") => new()
    {
        EventId = id, OrderId = "ORDER1", SupplierId = supplier, StatusCode = status,
        Description = description, Location = "杭州", OccurredAt = ShippedAt.AddMinutes(minutes), TemperatureCelsius = temperature
    };
    private static LogisticsStore Store() => new()
    {
        Committed = new() { Deliveries = [new() { DeliveryID = "DEL1", OrderID = "ORDER1", SupplierID = "SUP1",
            ShippedAt = ShippedAt, TrackingNo = "BASE-TRACK", CarrierName = "基础承运商" }] }
    };
    private static async Task CommitEvent(LogisticsStore store, LogisticsTrackingEventCommand command)
    {
        using var transaction = new FakeOrderTransaction();
        await Service(store).AppendTrackingEventAsync(command, transaction);
        AssertEx.True(!transaction.Committed && !transaction.RolledBack);
        transaction.Commit();
    }

    private static async Task RegistrationAsync()
    {
        var store = Store();
        using var transaction = new FakeOrderTransaction();
        var result = await Service(store).RegisterShipmentAsync(Registration(DateTime.Now.AddHours(2)), transaction);
        AssertEx.Equal("TRACK-EXTERNAL", result.TrackingNo);
        AssertEx.Equal(0, store.Committed.Deliveries.Single().IsRegistered);
        AssertEx.True(!transaction.Committed && !transaction.RolledBack);
        transaction.Commit();
        var fresh = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal("TRACK-EXTERNAL", fresh.TrackingNo);
        AssertEx.Equal(result.Events.Single().EventId, fresh.Events.Single().EventId);
        AssertEx.Equal(LogisticsDataSources.GroupA, fresh.DataSource);
        using var retry = new FakeOrderTransaction();
        await Service(store).RegisterShipmentAsync(Registration(), retry);
        retry.Commit();
        AssertEx.Equal(1, store.Committed.Events.Count);
    }

    private static async Task RegistrationRollbackAsync()
    {
        var store = Store();
        using var transaction = new FakeOrderTransaction();
        await Service(store).RegisterShipmentAsync(Registration(), transaction);
        transaction.Rollback();
        AssertEx.Equal(0, store.Committed.Deliveries.Single().IsRegistered);
        AssertEx.Equal(0, store.Committed.Events.Count);
        AssertEx.Equal("BASE-TRACK", (await Service(store).GetSnapshotAsync(Seed())).TrackingNo);
    }

    private static async Task AppendAsync()
    {
        var store = Store();
        await CommitEvent(store, Event());
        var fresh = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal(LogisticsStatusCodes.InTransit, fresh.StatusCode);
        AssertEx.Equal(LogisticsStatusCodes.InTransit, store.Committed.Deliveries.Single().LogisticsStatus);
        AssertEx.Equal(2, fresh.Events.Count);
        AssertEx.Equal(Event().OccurredAt, fresh.Events.Last().OccurredAt);
        AssertEx.Equal(1, store.Committed.Deliveries.Single().IsRegistered);
    }

    private static async Task AppendRollbackAsync()
    {
        var store = Store();
        var repository = new TransactionalLogisticsRepository(store) { FailStatusUpdate = true };
        var service = new OracleGroupALogisticsExtensionProvider(repository, Options.Create(new GroupALogisticsOptions()));
        using var transaction = new FakeOrderTransaction();
        await AssertEx.ThrowsAsync<InvalidOperationException>(() => service.AppendTrackingEventAsync(Event(), transaction));
        AssertEx.True(!transaction.Committed && !transaction.RolledBack);
        transaction.Rollback();
        AssertEx.Equal(0, store.Committed.Events.Count);
        AssertEx.Equal(0, store.Committed.Deliveries.Single().IsRegistered);
        AssertEx.Equal(LogisticsStatusCodes.Shipped, store.Committed.Deliveries.Single().LogisticsStatus);
    }

    private static async Task IdempotencyAsync()
    {
        var store = Store();
        await CommitEvent(store, Event());
        await CommitEvent(store, Event("EVENT2", LogisticsStatusCodes.Delivered, 2));
        await CommitEvent(store, Event());
        await CommitEvent(store, Event(temperature: 4.00m));
        AssertEx.Equal(3, store.Committed.Events.Count);
        AssertEx.Equal(LogisticsStatusCodes.Delivered, (await Service(store).GetSnapshotAsync(Seed())).StatusCode);
        using var transaction = new FakeOrderTransaction();
        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            Service(store).AppendTrackingEventAsync(Event(description: "被修改的内容"), transaction));
        transaction.Rollback();
        AssertEx.Equal(3, store.Committed.Events.Count);
    }

    private static async Task OwnershipAsync()
    {
        var store = Store();
        store.Committed.Deliveries.Add(new() { DeliveryID = "DEL2", OrderID = "ORDER1", SupplierID = "SUP2", ShippedAt = ShippedAt });
        await CommitEvent(store, Event());
        using var transaction = new FakeOrderTransaction();
        await AssertEx.ThrowsAsync<OrderBusinessException>(() => Service(store).AppendTrackingEventAsync(Event(supplier: "SUP2"), transaction));
        await AssertEx.ThrowsAsync<OrderBusinessException>(() => Service(store).RegisterShipmentAsync(Registration(deliveryId: "DEL2"), transaction));
        await AssertEx.ThrowsAsync<OrderBusinessException>(() => Service(store).AppendTrackingEventAsync(Event(supplier: "SUP3"), transaction));
        transaction.Rollback();
        AssertEx.Equal(LogisticsStatusCodes.Pending, (await Service(store).GetSnapshotAsync(Seed("SUP3"))).StatusCode);
        using var firstShipment = new FakeOrderTransaction();
        var registered = Store();
        registered.Committed.Deliveries.Add(store.Committed.Deliveries.Last());
        await Service(registered).RegisterShipmentAsync(Registration(), firstShipment);
        firstShipment.Commit();
        using var conflicting = new FakeOrderTransaction();
        await AssertEx.ThrowsAsync<OrderBusinessException>(() => Service(registered).RegisterShipmentAsync(new()
        {
            OrderId = "ORDER1", SupplierId = "SUP2", DeliveryId = "DEL2",
            Command = new() { SupplierId = "SUP2", CarrierCode = "TEST", TrackingNo = "TRACK-EXTERNAL" }
        }, conflicting));
        conflicting.Rollback();
        AssertEx.Equal(1, registered.Committed.Deliveries.Single(item => item.DeliveryID == "DEL1").IsRegistered);
    }

    private static async Task TemperatureAsync()
    {
        var store = Store();
        await CommitEvent(store, Event());
        await CommitEvent(store, Event("HOT", LogisticsStatusCodes.Delivered, 2, 20m));
        var hot = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal(LogisticsStatusCodes.Exception, hot.StatusCode);
        AssertEx.True(hot.HasException && hot.Events.Last().IsTemperatureException && hot.DeliveredAt == null);
        await CommitEvent(store, Event("RECOVER", minutes: 3));
        var recovered = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.True(!recovered.HasException && recovered.Events.Any(item => item.IsTemperatureException));
        await CommitEvent(store, Event("DELIVERED", LogisticsStatusCodes.Delivered, 4));
        AssertEx.Equal<DateTime?>(ShippedAt.AddMinutes(4), (await Service(store).GetSnapshotAsync(Seed())).DeliveredAt);
        using var transaction = new FakeOrderTransaction();
        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            Service(store).AppendTrackingEventAsync(Event("HOT-TERMINAL", LogisticsStatusCodes.Delivered, 5, 20m), transaction));
        transaction.Rollback();
    }

    private static async Task DelayAsync()
    {
        var store = Store();
        using var transaction = new FakeOrderTransaction();
        await Service(store).RegisterShipmentAsync(Registration(ShippedAt.AddMinutes(10)), transaction);
        transaction.Commit();
        var first = await Service(store).GetSnapshotAsync(Seed());
        var second = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal(LogisticsStatusCodes.Exception, first.StatusCode);
        AssertEx.Equal(first.Events.Last().EventId, second.Events.Last().EventId);
        AssertEx.Equal(first.Events.Last().OccurredAt, second.Events.Last().OccurredAt);
        AssertEx.Equal(1, store.Committed.Events.Count);
        await CommitEvent(store, Event(minutes: 20));
        AssertEx.Equal(3, store.Committed.Events.Count);
        var resumed = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal(LogisticsStatusCodes.InTransit, resumed.StatusCode);
        AssertEx.Equal(3, resumed.Events.Count);
    }

    private static async Task LegacyAsync()
    {
        var store = Store();
        store.Committed.Deliveries.Single().LogisticsStatus = "已签收";
        var snapshot = await Service(store).GetSnapshotAsync(Seed());
        AssertEx.Equal(LogisticsStatusCodes.Delivered, snapshot.StatusCode);
        AssertEx.True(snapshot.DeliveredAt == null);
        AssertEx.Equal(snapshot.Events.Single().EventId, (await Service(store).GetSnapshotAsync(Seed())).Events.Single().EventId);
        AssertEx.Equal(0, store.Committed.Events.Count);
        store.Committed.Deliveries.Single().LogisticsStatus = "运输中";
        await CommitEvent(store, Event("LEGACY-DELIVERY", LogisticsStatusCodes.Delivered));
        AssertEx.Equal(2, store.Committed.Events.Count);
    }

    private static async Task ValidationAsync()
    {
        var store = Store();
        await CommitEvent(store, Event());
        foreach (var command in new[] { Event("OLD", minutes: 0), Event("FUTURE", minutes: 300),
            Event("BAD", LogisticsStatusCodes.Shipped), Event("UNKNOWN", "UNKNOWN") })
        {
            using var transaction = new FakeOrderTransaction();
            await AssertEx.ThrowsAsync<OrderBusinessException>(() => Service(store).AppendTrackingEventAsync(command, transaction));
            transaction.Rollback();
        }
        AssertEx.Equal(2, store.Committed.Events.Count);
    }

    private static async Task ProviderSelectionAsync()
    {
        ServiceProvider Build(string? selected, string environment)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["GroupA:Logistics:Provider"] = selected ?? "Oracle" }).Build();
            var services = new ServiceCollection();
            services.AddSingleton(FinancialProxy.Create<IHostEnvironment>((method, _) => method.Name == "get_EnvironmentName"
                ? environment : throw new NotSupportedException(method.Name)));
            services.AddGroupALogisticsPersistence(config);
            services.AddScoped<IGroupALogisticsRepository>(_ => new TransactionalLogisticsRepository(Store()));
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
        using var production = Build(null, Environments.Production);
        using var scope = production.CreateScope();
        AssertEx.True(scope.ServiceProvider.GetRequiredService<IGroupALogisticsExtensionProvider>() is OracleGroupALogisticsExtensionProvider);
        foreach (var (selected, environment) in new[] { ("Fallback", Environments.Production), ("Fallback", Environments.Staging), ("Invalid", Environments.Development) })
        {
            using var rejected = Build(selected, environment);
            await AssertEx.ThrowsAsync<OptionsValidationException>(() => Task.FromResult(
                rejected.GetRequiredService<IOptions<GroupALogisticsOptions>>().Value));
        }
        using var development = Build("Fallback", Environments.Development);
        AssertEx.Equal("Fallback", development.GetRequiredService<IOptions<GroupALogisticsOptions>>().Value.Provider);
    }

    private static async Task BRetryAsync()
    {
        var called = false;
        var orders = new FakeOrderRepository();
        orders.Orders.Add(new() { OrderId = "ORDER1", OrderStatus = OrderStatusCodes.Shipped });
        orders.Details.Add(new() { OrderId = "ORDER1", SupplierId = "SUP1" });
        var logistics = FinancialProxy.Create<ILogisticsService>((method, _) => method.Name switch
        {
            nameof(ILogisticsService.GetSupplierLogisticsAsync) => Task.FromResult<IReadOnlyList<SupplierLogisticsSnapshot>>(
                [new() { OrderId = "ORDER1", SupplierId = "SUP1", StatusCode = LogisticsStatusCodes.Delivered,
                    Events = [new() { EventId = "EVENT1" }] }]),
            nameof(ILogisticsService.AppendTrackingEventAsync) => Reject(),
            _ => throw new NotSupportedException(method.Name)
        });
        Task<SupplierLogisticsSnapshot> Reject() { called = true; throw new OrderBusinessException("同编号载荷不同"); }
        var manager = new FakeTransactionManager();
        var service = new SupplierFulfillmentService(orders, logistics, manager);
        await AssertEx.ThrowsAsync<OrderBusinessException>(() => service.AppendTrackingEventAsync("SUP1", "ORDER1", Event()));
        AssertEx.True(called && manager.LastTransaction?.RolledBack == true);
    }
}

// 仅模拟顺序事务隔离与回滚，不代表 Oracle 的并发锁或持久存储验证。
internal sealed class LogisticsStore
{
    public LogisticsStoreState Committed { get; set; } = new();
}
internal sealed class LogisticsStoreState
{
    public List<LogExpressDelivery> Deliveries { get; set; } = [];
    public List<LogLogisticsEvent> Events { get; set; } = [];
}
internal sealed class TransactionalLogisticsRepository(LogisticsStore store) : IGroupALogisticsRepository
{
    private readonly Dictionary<IDbTransaction, LogisticsStoreState> staged = [];
    private readonly HashSet<(IDbTransaction Transaction, string Delivery)> locks = [];
    public bool FailStatusUpdate { get; init; }
    private LogisticsStoreState State(IDbTransaction? transaction)
    {
        if (transaction == null) return store.Committed;
        if (!staged.TryGetValue(transaction, out var state))
        {
            state = JsonSerializer.Deserialize<LogisticsStoreState>(JsonSerializer.Serialize(store.Committed))!;
            staged[transaction] = state;
            ((FakeOrderTransaction)transaction).Stage(() => store.Committed = state);
        }
        return state;
    }
    private LogisticsStoreState Locked(string deliveryId, IDbTransaction transaction)
    {
        if (!locks.Contains((transaction, deliveryId))) throw new InvalidOperationException("写入前必须锁定基础发货单");
        return State(transaction);
    }
    public Task<LogExpressDelivery?> GetDeliveryAsync(string orderId, string supplierId, bool forUpdate,
        IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var delivery = State(transaction).Deliveries.SingleOrDefault(item => item.OrderID == orderId && item.SupplierID == supplierId);
        if (forUpdate && delivery != null) locks.Add((transaction!, delivery.DeliveryID));
        return Task.FromResult(delivery);
    }
    public Task<IReadOnlyList<LogLogisticsEvent>> GetEventsAsync(string deliveryId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LogLogisticsEvent>>(State(transaction).Events.Where(item => item.DeliveryId == deliveryId).OrderBy(item => item.SequenceNo).ToList());
    public Task<LogLogisticsEvent?> GetEventAsync(string eventId, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
        Task.FromResult(State(transaction).Events.SingleOrDefault(item => item.EventId == eventId));
    public Task UpdateDetailAsync(LogExpressDelivery detail, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var state = Locked(detail.DeliveryID, transaction);
        if (detail.CarrierTrackingKey != null && state.Deliveries.Any(item =>
            item.DeliveryID != detail.DeliveryID && item.CarrierTrackingKey == detail.CarrierTrackingKey))
            throw new LogisticsWriteConflictException("运单冲突");
        var target = state.Deliveries.Single(item => item.DeliveryID == detail.DeliveryID);
        target.CarrierCode = detail.CarrierCode;
        target.CarrierName = detail.CarrierName;
        target.TrackingNo = detail.TrackingNo;
        target.PackageTemp = detail.PackageTemp;
        target.CarrierTrackingKey = detail.CarrierTrackingKey;
        target.EstimatedArrivalAt = detail.EstimatedArrivalAt;
        target.Remark = detail.Remark;
        target.IsRegistered = 1;
        return Task.CompletedTask;
    }
    public Task InsertEventAsync(LogLogisticsEvent item, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var state = Locked(item.DeliveryId, transaction);
        if (state.Events.Any(previous => previous.EventId == item.EventId ||
            previous.DeliveryId == item.DeliveryId && previous.SequenceNo == item.SequenceNo)) throw new LogisticsWriteConflictException("事件冲突");
        state.Events.Add(item);
        return Task.CompletedTask;
    }
    public Task UpdateBaseStatusAsync(string deliveryId, string status, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (FailStatusUpdate) throw new InvalidOperationException("模拟基础状态更新失败");
        Locked(deliveryId, transaction).Deliveries.Single(item => item.DeliveryID == deliveryId).LogisticsStatus = status;
        return Task.CompletedTask;
    }
}
