using FreshColdChain.Models;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class OrderLifecycleScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("订单列表按状态和关键词分页查询", OrderListFiltersAndPagesAsync),
            ("订单详情按供应商形成拆单展示", OrderDetailGroupsBySupplierAsync),
            ("下单使用运费契约并保存地址快照", CreateOrderUsesFreightAndAddressSnapshotAsync),
            ("已支付订单发货时同步创建物流", PaidOrderShipsAsync),
            ("已发货订单完成时触发佣金登记", ShippedOrderCompletesAsync),
            ("非法状态跳转被拒绝并回滚", InvalidTransitionRollsBackAsync),
            ("佣金登记失败时订单完成回滚", CommissionFailureRollsBackAsync),
            ("取消订单归还库存和营销资产", CancellationCompensatesAssetsAsync),
            ("库存释放失败时取消订单整体回滚", ReleaseFailureRollsBackCancellationAsync),
            ("未发货退款幂等释放库存并阻止再次发货", PaidRefundIsIdempotentAndPreventsShipmentAsync),
            ("已发货退款不错误回补库存", ShippedRefundDoesNotReleaseInventoryAsync),
            ("退款消费者与订单不匹配时整体回滚", RefundCustomerMismatchRollsBackAsync)
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
            $"订单生命周期场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task OrderListFiltersAndPagesAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-PAID-001");
        SeedOrder(context, 2, OrderStatus.Completed, "ORD-DONE-002");

        var result = await context.Service.GetOrdersAsync(new OrderQueryRequest
        {
            Status = OrderStatus.Completed,
            Keyword = "DONE",
            Page = 1,
            PageSize = 1
        });

        AssertEx.Equal(1, result.TotalCount);
        AssertEx.Equal(1, result.TotalPages);
        AssertEx.Equal(1, result.Orders.Count);
        AssertEx.Equal(2, result.Orders[0].OrderId);
        AssertEx.Equal("已完成", result.Orders[0].StatusName);
        AssertEx.Equal(2, result.Orders[0].SupplierCount);
    }

    private static async Task OrderDetailGroupsBySupplierAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-GROUP-001");

        var result = await context.Service.GetOrderDetailAsync(1);

        AssertEx.True(result != null);
        AssertEx.Equal(2, result!.SupplierGroups.Count);
        AssertEx.Equal(50m, result.SupplierGroups[0].SubTotal);
        AssertEx.Equal(80m, result.SupplierGroups[1].SubTotal);
        AssertEx.Equal("待发货", result.SupplierGroups[0].FulfillmentStatus);
        AssertEx.Equal("TRACK-1-1", result.SupplierGroups[0].TrackingNo);
        AssertEx.True(result.CanShip);
        AssertEx.True(result.CanCancel);
        AssertEx.True(!result.CanComplete);
    }

    private static async Task CreateOrderUsesFreightAndAddressSnapshotAsync()
    {
        var context = TestContext.Create();
        context.LogisticsService.FreightAmount = 15m;

        var result = await context.Service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 1,
            AddressId = 11,
            Items = [new() { ProductId = 1, Quantity = 2 }]
        });

        var order = context.OrderRepository.Orders.Single();
        AssertEx.Equal(15m, result.FreightAmount);
        AssertEx.Equal(115m, result.FinalAmount);
        AssertEx.Equal("默认收件人", order.ReceiverName);
        AssertEx.Equal("13800138000", order.ReceiverPhone);
        AssertEx.True(order.ShippingAddress.Contains("浙江省", StringComparison.Ordinal));
        AssertEx.Equal(1, context.LogisticsService.LastFreightRequest?.Items.Count ?? 0);
        AssertCommitted(context);
    }

    private static async Task PaidOrderShipsAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-SHIP-001");

        await context.Service.TransitionOrderAsync(1, OrderStatus.Shipped);

        AssertEx.Equal(
            (int)OrderStatus.Shipped,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.LogisticsService.ShippedOrderIds.Count);
        AssertEx.Equal(1, context.LogisticsService.ShippedOrderIds[0]);
        AssertCommitted(context);
    }

    private static async Task ShippedOrderCompletesAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.PromoterId = 9;
        SeedOrder(context, 1, OrderStatus.Shipped, "ORD-COMPLETE-001");

        await context.Service.TransitionOrderAsync(1, OrderStatus.Completed);

        AssertEx.Equal(
            (int)OrderStatus.Completed,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.CommissionService.CompletedOrders.Count);
        var commission = context.CommissionService.CompletedOrders[0];
        AssertEx.Equal(9, commission.PromoterId);
        AssertEx.Equal(130m, commission.CommissionBaseAmount);
        AssertCommitted(context);
    }

    private static async Task InvalidTransitionRollsBackAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-INVALID-001");

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(1, OrderStatus.Completed));

        AssertEx.Equal(
            (int)OrderStatus.Paid,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
        AssertRolledBack(context);
    }

    private static async Task CommissionFailureRollsBackAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, 1, OrderStatus.Shipped, "ORD-COMM-FAIL-001");
        context.CommissionService.ExceptionToThrow =
            new InvalidOperationException("模拟佣金登记失败");

        await AssertEx.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.TransitionOrderAsync(1, OrderStatus.Completed));

        AssertEx.Equal(
            (int)OrderStatus.Shipped,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
        AssertRolledBack(context);
    }

    private static async Task CancellationCompensatesAssetsAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        context.CustomerRepository.Customer.TotalSpent = 130m;
        context.CustomerRepository.Customer.MemberLevelId = 2;
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-CANCEL-001", pointsEarned: 30);
        context.CouponRepository.Records.Add(new MktCouponRecord
        {
            RecordId = 7,
            CouponId = 3,
            CustomerId = 1,
            OrderId = 1,
            Status = 1,
            UsedAt = DateTime.Now,
            CreatedAt = DateTime.Now.AddMinutes(-5)
        });

        await context.Service.CancelOrderAsync(1);

        AssertEx.Equal(
            (int)OrderStatus.Cancelled,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.Equal(1, context.CustomerRepository.Customer.MemberLevelId);
        AssertEx.Equal(0, context.CouponRepository.Records[0].Status);
        AssertEx.Equal(null, context.CouponRepository.Records[0].OrderId);
        AssertEx.Equal(1, context.InventoryService.ReleasedOrderIds.Count);
        AssertEx.Equal(-30, context.PointRepository.Logs.Single().ChangeAmount);
        AssertCommitted(context);
    }

    private static async Task ReleaseFailureRollsBackCancellationAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        context.CustomerRepository.Customer.TotalSpent = 130m;
        SeedOrder(context, 1, OrderStatus.Paid, "ORD-RELEASE-FAIL-001", pointsEarned: 30);
        context.InventoryService.ReleaseExceptionToThrow =
            new InvalidOperationException("模拟库存释放失败");

        await AssertEx.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.CancelOrderAsync(1));

        AssertEx.Equal(
            (int)OrderStatus.Paid,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(130, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(130m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.Equal(0, context.InventoryService.ReleasedOrderIds.Count);
        AssertEx.Equal(0, context.PointRepository.Logs.Count);
        AssertRolledBack(context);
    }

    private static async Task PaidRefundIsIdempotentAndPreventsShipmentAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            1,
            OrderStatus.Paid,
            "ORD-REFUND-PAID-001",
            pointsEarned: 30);

        await context.Service.DeductPointsForRefundAsync(1, 1, 50);
        await context.Service.DeductPointsForRefundAsync(1, 1, 50);

        AssertEx.Equal(
            (int)OrderStatus.Refunded,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(1, context.InventoryService.ReleasedOrderIds.Count);
        AssertEx.Equal(1, context.PointRepository.Logs.Count);
        AssertEx.Equal(-30, context.PointRepository.Logs[0].ChangeAmount);

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(1, OrderStatus.Shipped));
        AssertEx.Equal(0, context.LogisticsService.ShippedOrderIds.Count);
    }

    private static async Task ShippedRefundDoesNotReleaseInventoryAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            1,
            OrderStatus.Shipped,
            "ORD-REFUND-SHIPPED-001",
            pointsEarned: 30);

        await context.Service.DeductPointsForRefundAsync(1, 1, 30);

        AssertEx.Equal(
            (int)OrderStatus.Refunded,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0, context.InventoryService.ReleasedOrderIds.Count);
        AssertCommitted(context);
    }

    private static async Task RefundCustomerMismatchRollsBackAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            1,
            OrderStatus.Paid,
            "ORD-REFUND-MISMATCH-001",
            pointsEarned: 30);

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.DeductPointsForRefundAsync(2, 1, 30));

        AssertEx.Equal(
            (int)OrderStatus.Paid,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(130, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0, context.InventoryService.ReleasedOrderIds.Count);
        AssertEx.Equal(0, context.PointRepository.Logs.Count);
        AssertRolledBack(context);
    }

    private static void SeedOrder(
        TestContext context,
        int orderId,
        OrderStatus status,
        string orderNo,
        int pointsEarned = 0)
    {
        context.OrderRepository.Orders.Add(new BizOrder
        {
            OrderId = orderId,
            OrderNo = orderNo,
            CustomerId = 1,
            AddressId = 11,
            ReceiverName = "默认收件人",
            ReceiverPhone = "13800138000",
            ShippingAddress = "浙江省 杭州市 西湖区 测试路1号",
            TotalAmount = 130m,
            DiscountAmount = 0m,
            FreightAmount = 0m,
            FinalAmount = 130m,
            PointsEarned = pointsEarned,
            OrderStatus = (int)status,
            CreatedAt = DateTime.Now.AddMinutes(-orderId)
        });
        context.OrderRepository.Details.AddRange(
        [
            new BizOrderDetail
            {
                OrderDetailId = orderId * 10 + 1,
                OrderId = orderId,
                ProductId = 1,
                ProductName = "车厘子",
                Quantity = 1,
                UnitPrice = 50m,
                SubTotal = 50m,
                SupplierId = 1
            },
            new BizOrderDetail
            {
                OrderDetailId = orderId * 10 + 2,
                OrderId = orderId,
                ProductId = 2,
                ProductName = "三文鱼",
                Quantity = 1,
                UnitPrice = 80m,
                SubTotal = 80m,
                SupplierId = 2
            }
        ]);
    }

    private static void AssertCommitted(TestContext context)
    {
        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == true);
        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == false);
    }

    private static void AssertRolledBack(TestContext context)
    {
        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == false);
        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == true);
    }
}
