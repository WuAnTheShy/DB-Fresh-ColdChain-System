using FreshColdChain.Models;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class SupplierFulfillmentScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("供应商履约列表只返回本人订单", ListOnlyReturnsOwnedOrdersAsync),
            ("供应商不能查看其他供应商履约明细", DetailRejectsOtherSupplierAsync),
            ("多供应商分别发货后订单才进入已发货", MultiSupplierShipmentCompletesOrderAsync),
            ("重复提交同一供应商发货保持幂等", RepeatedSupplierShipmentIsIdempotentAsync),
            ("已发货供应商可以追加合法物流轨迹", ShippedSupplierAppendsTrackingEventAsync),
            ("非法物流状态跳转被拒绝并回滚", InvalidLogisticsTransitionRollsBackAsync)
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
            $"供应商履约场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task ListOnlyReturnsOwnedOrdersAsync()
    {
        var context = CreateContext();

        var result = await context.Service.GetOrdersAsync(
            "SUP1",
            new SupplierFulfillmentQuery());

        AssertEx.Equal(1, result.TotalCount);
        AssertEx.Equal(TestIds.Order, result.Orders.Single().OrderId);
        AssertEx.Equal(1, result.Orders.Single().ItemCount);
        AssertEx.Equal(50m, result.Orders.Single().SupplierAmount);
    }

    private static async Task DetailRejectsOtherSupplierAsync()
    {
        var context = CreateContext();
        context.OrderRepository.Details.RemoveAll(detail => detail.SupplierId == "SUP2");

        var result = await context.Service.GetOrderAsync("SUP2", TestIds.Order);

        AssertEx.Equal<SupplierFulfillmentDetailViewModel?>(null, result);
    }

    private static async Task MultiSupplierShipmentCompletesOrderAsync()
    {
        var context = CreateContext();

        await context.Service.ShipAsync(
            "SUP1",
            TestIds.Order,
            CreateCommand("SUP1", "TRACK-1"));
        AssertEx.Equal(OrderStatusCodes.Paid, context.OrderRepository.Orders.Single().OrderStatus);

        await context.Service.ShipAsync(
            "SUP2",
            TestIds.Order,
            CreateCommand("SUP2", "TRACK-2"));

        AssertEx.Equal(OrderStatusCodes.Shipped, context.OrderRepository.Orders.Single().OrderStatus);
        AssertEx.Equal(2, context.LogisticsService.ShippedOrderIds.Count);
        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == true);
    }

    private static async Task RepeatedSupplierShipmentIsIdempotentAsync()
    {
        var context = CreateContext();
        var command = CreateCommand("SUP1", "TRACK-1");

        await context.Service.ShipAsync("SUP1", TestIds.Order, command);
        await context.Service.ShipAsync("SUP1", TestIds.Order, command);

        AssertEx.Equal(1, context.LogisticsService.ShippedOrderIds.Count);
    }

    private static async Task ShippedSupplierAppendsTrackingEventAsync()
    {
        var context = CreateContext();
        await context.Service.ShipAsync("SUP1", TestIds.Order, CreateCommand("SUP1", "TRACK-1"));

        var result = await context.Service.AppendTrackingEventAsync(
            "SUP1",
            TestIds.Order,
            CreateEvent(LogisticsStatusCodes.InTransit));

        AssertEx.Equal(LogisticsStatusCodes.InTransit, result.StatusCode);
        AssertEx.Equal(1, result.Events.Count);
        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == true);
    }

    private static async Task InvalidLogisticsTransitionRollsBackAsync()
    {
        var context = CreateContext();
        await context.Service.ShipAsync("SUP1", TestIds.Order, CreateCommand("SUP1", "TRACK-1"));
        await context.Service.ShipAsync("SUP2", TestIds.Order, CreateCommand("SUP2", "TRACK-2"));

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.AppendTrackingEventAsync(
                "SUP1",
                TestIds.Order,
                CreateEvent(LogisticsStatusCodes.Returned)));

        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == true);
    }

    private static LogisticsTrackingEventCommand CreateEvent(string status) => new()
    {
        OrderId = TestIds.Order,
        SupplierId = "SUP1",
        StatusCode = status,
        Location = "杭州冷链中转场",
        Description = "包裹已到达中转场",
        OccurredAt = DateTime.Now,
        TemperatureCelsius = 4m
    };

    private static SupplierShipmentCommand CreateCommand(string supplierId, string trackingNo) =>
        new()
        {
            SupplierId = supplierId,
            CarrierCode = "TEST",
            CarrierName = "测试承运商",
            TrackingNo = trackingNo,
            PackageTemperature = "CHILLED",
            EstimatedArrivalAt = DateTime.Now.AddDays(1)
        };

    private static SupplierFulfillmentTestContext CreateContext()
    {
        var repository = new FakeOrderRepository();
        repository.Orders.Add(new BizOrder
        {
            OrderId = TestIds.Order,
            OrderNo = "ORD-SUPPLIER-1",
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            ReceiverName = "默认收件人",
            ReceiverPhone = "13800138000",
            ReceiverProvince = "浙江省",
            ReceiverCity = "杭州市",
            ReceiverDistrict = "西湖区",
            ReceiverDetailAddress = "测试路1号",
            TotalAmount = 130m,
            FinalAmount = 130m,
            OrderStatus = OrderStatusCodes.Paid,
            CreatedAt = DateTime.Now.AddMinutes(-5)
        });
        repository.Details.AddRange(
        [
            new BizOrderDetail
            {
                OrderDetailId = "detail-sup-1",
                OrderId = TestIds.Order,
                ProductId = "P1",
                ProductName = "车厘子",
                SupplierId = "SUP1",
                Quantity = 1,
                UnitPrice = 50m,
                SubTotal = 50m
            },
            new BizOrderDetail
            {
                OrderDetailId = "detail-sup-2",
                OrderId = TestIds.Order,
                ProductId = "P2",
                ProductName = "三文鱼",
                SupplierId = "SUP2",
                Quantity = 1,
                UnitPrice = 80m,
                SubTotal = 80m
            }
        ]);
        var logistics = new FakeLogisticsService();
        var transactions = new FakeTransactionManager();
        return new SupplierFulfillmentTestContext(
            repository,
            logistics,
            transactions,
            new SupplierFulfillmentService(repository, logistics, transactions));
    }

    private sealed record SupplierFulfillmentTestContext(
        FakeOrderRepository OrderRepository,
        FakeLogisticsService LogisticsService,
        FakeTransactionManager TransactionManager,
        SupplierFulfillmentService Service);
}
