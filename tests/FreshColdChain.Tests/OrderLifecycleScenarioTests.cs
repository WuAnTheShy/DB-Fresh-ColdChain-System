using System.Text.Json;
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
            ("消费者订单展示状态跟随物流变化", OrderDisplayStatusTracksLogisticsAsync),
            ("下单使用运费契约并保存地址快照", CreateOrderUsesFreightAndAddressSnapshotAsync),
            ("已支付订单发货时同步创建物流", PaidOrderShipsAsync),
            ("已发货订单完成时触发佣金登记", ShippedOrderCompletesAsync),
            ("已发货商品逐项确认且最后一项自动完成子订单", ItemReceiptCompletesAfterLastItemAsync),
            ("整单确认收货一次更新全部商品", OrderReceiptConfirmsEveryItemAsync),
            ("未签收包裹不能通过后端直接确认收货", ReceiptRejectsUndeliveredPackageAsync),
            ("自动收货必须等待全部包裹签收", AutoReceiptRequiresDeliveredPackagesAsync),
            ("管理端不能完成未签收订单", AdminCompletionRequiresDeliveredPackagesAsync),
            ("退款参与外部事务且由调用方提交回滚", RefundUsesCallerTransactionAsync),
            ("退款写入失败时不接管调用方事务", RefundFailurePreservesCallerTransactionAsync),
            ("非法状态跳转被拒绝并回滚", InvalidTransitionRollsBackAsync),
            ("佣金登记失败时订单完成回滚", CommissionFailureRollsBackAsync),
            ("取消订单归还营销资产", CancellationCompensatesAssetsAsync),
            ("未发货退款幂等处理并阻止再次发货", PaidRefundIsIdempotentAndPreventsShipmentAsync),
            ("已发货退款正确扣回营销资产", ShippedRefundDeductsAssetsAsync),
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
        SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-PAID-001");
        SeedOrder(context, TestIds.Order2, OrderStatus.Completed, "ORD-DONE-002");

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
        AssertEx.Equal(TestIds.Order2, result.Orders[0].OrderId);
        AssertEx.Equal("已完成", result.Orders[0].StatusName);
        AssertEx.Equal(2, result.Orders[0].SupplierCount);
        AssertEx.Equal(2, result.Orders[0].ProductItems.Count);
        AssertEx.Equal("车厘子", result.Orders[0].ProductItems[0].ProductName);
    }

    private static async Task OrderDetailGroupsBySupplierAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-GROUP-001");
        context.LogisticsService.ShippedSupplierKeys.Add(
            $"{TestIds.Order}|SUP1");

        var result = await context.Service.GetOrderDetailAsync(TestIds.Order);

        AssertEx.True(result != null);
        AssertEx.Equal(2, result!.SupplierGroups.Count);
        AssertEx.Equal(50m, result.SupplierGroups[0].SubTotal);
        AssertEx.Equal(80m, result.SupplierGroups[1].SubTotal);
        AssertEx.Equal("已发货", result.SupplierGroups[0].FulfillmentStatus);
        AssertEx.Equal($"TRACK-{TestIds.Order}-SUP1", result.SupplierGroups[0].TrackingNo);
        AssertEx.Equal("FROZEN", result.SupplierGroups[0].Logistics.PackageTemperature);
        AssertEx.Equal("测试冷链", result.SupplierGroups[0].Logistics.CarrierName);
        AssertEx.Equal(1, result.SupplierGroups[0].Logistics.Events.Count);
        AssertEx.Equal(-20m, result.SupplierGroups[0].Logistics.Events[0].TemperatureCelsius ?? 0m);
        AssertEx.True(result.CanShip);
        AssertEx.True(result.CanCancel);
        AssertEx.True(!result.CanComplete);
    }

    private static async Task OrderDisplayStatusTracksLogisticsAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-LOGISTICS-001");
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.InTransit;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.InTransit;

        var list = await context.Service.GetOrdersAsync(new OrderQueryRequest());
        var detail = await context.Service.GetOrderDetailAsync(TestIds.Order);

        AssertEx.Equal(LogisticsStatusCodes.InTransit, list.Orders.Single().DisplayStatusCode);
        AssertEx.Equal("运输中", list.Orders.Single().StatusName);
        AssertEx.Equal(LogisticsStatusCodes.InTransit, detail!.DisplayStatusCode);
        AssertEx.Equal("运输中", detail.StatusName);

        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;

        list = await context.Service.GetOrdersAsync(new OrderQueryRequest());
        detail = await context.Service.GetOrderDetailAsync(TestIds.Order);

        AssertEx.Equal(LogisticsStatusCodes.Delivered, list.Orders.Single().DisplayStatusCode);
        AssertEx.Equal("已签收", list.Orders.Single().StatusName);
        AssertEx.Equal(LogisticsStatusCodes.Delivered, detail!.DisplayStatusCode);
        AssertEx.Equal("已签收", detail.StatusName);
        AssertEx.Equal(OrderStatusCodes.Shipped, detail.Order!.OrderStatus);
    }

    private static async Task CreateOrderUsesFreightAndAddressSnapshotAsync()
    {
        var context = TestContext.Create();
        context.LogisticsService.FreightAmount = 15m;

        var result = await context.Service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            Items = [new() { ProductId = "P1", SupplierId = "SUP1", Quantity = 2 }]
        });

        var order = context.OrderRepository.Orders.Single();
        AssertEx.Equal(15m, result.FreightAmount);
        AssertEx.Equal(115m, result.FinalAmount);
        AssertEx.True(result.FreightQuote != null);
        AssertEx.True(!string.IsNullOrWhiteSpace(order.FreightQuoteSnapshot));
        var freightQuote = JsonSerializer.Deserialize<FreightCalculationResult>(
            order.FreightQuoteSnapshot!);
        AssertEx.Equal(15m, freightQuote?.FreightAmount ?? -1m);
        AssertEx.Equal("浙江省", freightQuote?.Province);
        var detail = await context.Service.GetOrderDetailAsync(order.OrderId);
        AssertEx.Equal(15m, detail?.FreightQuote?.FreightAmount ?? -1m);
        AssertEx.Equal("测试运费规则", detail?.FreightQuote?.RuleSummary);
        AssertEx.Equal("默认收件人", order.ReceiverName);
        AssertEx.Equal("13800138000", order.ReceiverPhone);
        AssertEx.True(order.ShippingAddress.Contains("浙江省", StringComparison.Ordinal));
        AssertEx.Equal(1, context.LogisticsService.LastFreightRequest?.Items.Count ?? 0);
        AssertCommitted(context);
    }

    private static async Task PaidOrderShipsAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-SHIP-001");

        await context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Shipped);

        AssertEx.Equal(
            OrderStatusCodes.Shipped,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.LogisticsService.ShippedOrderIds.Count);
        AssertEx.Equal(TestIds.Order, context.LogisticsService.ShippedOrderIds[0]);
        AssertCommitted(context);
    }

    private static async Task ShippedOrderCompletesAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.PromoterId = "PROM9";
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-COMPLETE-001");
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;

        await context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Completed);

        AssertEx.Equal(
            OrderStatusCodes.Completed,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.CommissionService.CompletedOrders.Count);
        var commission = context.CommissionService.CompletedOrders[0];
        AssertEx.Equal("PROM9", commission.promoterID);
        AssertEx.Equal(130m, commission.finalAmount);
        AssertCommitted(context);
    }

    private static async Task ItemReceiptCompletesAfterLastItemAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.PromoterId = "customer-promoter";
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-ITEM-RECEIPT");
        context.OrderRepository.Orders[0].PromoterId = "order-promoter";
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;

        await context.Service.ConfirmOrderItemReceiptAsync(
            TestIds.Order,
            $"{TestIds.Order}-detail-1",
            TestIds.Customer);

        AssertEx.Equal("RECEIVED", context.OrderRepository.Details[0].ReceiptStatus);
        AssertEx.Equal(OrderStatusCodes.Shipped, context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);

        await context.Service.ConfirmOrderItemReceiptAsync(
            TestIds.Order,
            $"{TestIds.Order}-detail-2",
            TestIds.Customer);

        AssertEx.True(context.OrderRepository.Details.All(detail => detail.ReceiptStatus == "RECEIVED"));
        AssertEx.Equal(OrderStatusCodes.Completed, context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.CommissionService.CompletedOrders.Count);
        AssertEx.Equal("order-promoter", context.CommissionService.CompletedOrders[0].promoterID);
    }

    private static async Task OrderReceiptConfirmsEveryItemAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-ORDER-RECEIPT");
        context.OrderRepository.Orders[0].PromoterId = "order-promoter";
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;

        await context.Service.ConfirmOrderReceiptAsync(TestIds.Order, TestIds.Customer);

        AssertEx.True(context.OrderRepository.Details.All(detail => detail.ReceiptStatus == "RECEIVED"));
        AssertEx.Equal(OrderStatusCodes.Completed, context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.CommissionService.CompletedOrders.Count);
    }

    private static async Task ReceiptRejectsUndeliveredPackageAsync()
    {
        foreach (var status in new[] { LogisticsStatusCodes.Pending, LogisticsStatusCodes.Packing, LogisticsStatusCodes.Shipped,
            LogisticsStatusCodes.InTransit, LogisticsStatusCodes.OutForDelivery,
            LogisticsStatusCodes.Exception, LogisticsStatusCodes.Returning, LogisticsStatusCodes.Returned })
        {
            var context = TestContext.Create();
            SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-NO-RECEIPT");
            context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = status;
            // 其他供应商已签收不能代替当前商品所属包裹签收。
            context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;
            await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
                context.Service.ConfirmOrderItemReceiptAsync(
                    TestIds.Order, $"{TestIds.Order}-detail-1", TestIds.Customer));
            AssertEx.True(context.OrderRepository.Details.All(item => item.ReceiptStatus != "RECEIVED"));
            AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
            AssertRolledBack(context);
        }
    }

    private static async Task AutoReceiptRequiresDeliveredPackagesAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-AUTO-RECEIPT");
        context.OrderRepository.Orders[0].CreatedAt = DateTime.Now.AddDays(-8);
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Exception;
        AssertEx.Equal(0, await context.Service.AutoConfirmShippedOrdersAsync());
        AssertEx.True(context.OrderRepository.Details.All(item => item.ReceiptStatus != "RECEIVED"));
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);

        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;
        AssertEx.Equal(1, await context.Service.AutoConfirmShippedOrdersAsync());
        AssertEx.Equal(OrderStatusCodes.Completed, context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(1, context.CommissionService.CompletedOrders.Count);
    }

    private static async Task AdminCompletionRequiresDeliveredPackagesAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-ADMIN-RECEIPT");
        AssertEx.True(!(await context.Service.GetOrderDetailAsync(TestIds.Order))!.CanComplete);
        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Completed));
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
        AssertRolledBack(context);
    }

    private static async Task RefundUsesCallerTransactionAsync()
    {
        foreach (var partial in new[] { false, true })
        foreach (var commit in new[] { false, true })
        {
            var context = TestContext.Create();
            SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-REFUND-TX", pointsEarned: 30);
            context.CustomerRepository.Customer.Points = 100;
            using var transaction = new FakeOrderTransaction();
            if (partial)
                await context.Service.DeductPointsForPartialRefundAsync(
                    TestIds.Customer, TestIds.Order, 10, externalTransaction: transaction);
            else
                await context.Service.DeductPointsForRefundAsync(
                    TestIds.Customer, TestIds.Order, 10, externalTransaction: transaction);

            AssertEx.True(context.TransactionManager.LastTransaction == null);
            AssertEx.True(!transaction.Committed && !transaction.RolledBack);
            AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(OrderStatusCodes.Paid, context.OrderRepository.Orders[0].OrderStatus);
            if (commit) transaction.Commit();
            else transaction.Rollback();
            AssertEx.Equal(commit ? 90 : 100, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(commit ? (partial ? OrderStatusCodes.Refunding : OrderStatusCodes.Refunded)
                : OrderStatusCodes.Paid, context.OrderRepository.Orders[0].OrderStatus);
            AssertEx.Equal(commit ? 1 : 0, context.PointRepository.Logs.Count);
        }
    }

    private static async Task RefundFailurePreservesCallerTransactionAsync()
    {
        foreach (var partial in new[] { false, true })
        {
            var context = TestContext.Create();
            SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-REFUND-FAIL", pointsEarned: 30);
            context.CustomerRepository.Customer.Points = 100;
            context.PointRepository.ThrowOnInsert = true;
            using var transaction = new FakeOrderTransaction();

            await AssertEx.ThrowsAsync<InvalidOperationException>(() => partial
                ? context.Service.DeductPointsForPartialRefundAsync(
                    TestIds.Customer, TestIds.Order, 10, externalTransaction: transaction)
                : context.Service.DeductPointsForRefundAsync(
                    TestIds.Customer, TestIds.Order, 10, externalTransaction: transaction));

            // 异常必须交还发起方处理，B 组不能擅自结束整个跨组事务。
            AssertEx.True(context.TransactionManager.LastTransaction == null);
            AssertEx.True(!transaction.Committed && !transaction.RolledBack);
            transaction.Rollback();
            AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
            AssertEx.Equal(OrderStatusCodes.Paid, context.OrderRepository.Orders[0].OrderStatus);
            AssertEx.Equal(0, context.PointRepository.Logs.Count);
        }
    }

    private static async Task InvalidTransitionRollsBackAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-INVALID-001");

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Completed));

        AssertEx.Equal(
            OrderStatusCodes.Paid,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
        AssertRolledBack(context);
    }

    private static async Task CommissionFailureRollsBackAsync()
    {
        var context = TestContext.Create();
        SeedOrder(context, TestIds.Order, OrderStatus.Shipped, "ORD-COMM-FAIL-001");
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP1"] = LogisticsStatusCodes.Delivered;
        context.LogisticsService.SupplierStatuses[$"{TestIds.Order}|SUP2"] = LogisticsStatusCodes.Delivered;
        context.CommissionService.ReturnFailure = true;

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Completed));

        AssertEx.Equal(
            OrderStatusCodes.Shipped,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(0, context.CommissionService.CompletedOrders.Count);
        AssertRolledBack(context);
    }

    private static async Task CancellationCompensatesAssetsAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        context.CustomerRepository.Customer.TotalSpent = 130m;
        context.CustomerRepository.Customer.MemberLevelId = TestIds.Level2;
        SeedOrder(context, TestIds.Order, OrderStatus.Paid, "ORD-CANCEL-001", pointsEarned: 30);
        context.CouponRepository.Records.Add(new MktCouponRecord
        {
            RecordId = TestIds.Record,
            CouponId = TestIds.Coupon,
            CustomerId = TestIds.Customer,
            OrderId = TestIds.Order,
            Status = 1,
            UsedAt = DateTime.Now,
            CreatedAt = DateTime.Now.AddMinutes(-5)
        });

        await context.Service.CancelOrderAsync(TestIds.Order);

        AssertEx.Equal(
            OrderStatusCodes.Cancelled,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.Equal(TestIds.Level1, context.CustomerRepository.Customer.MemberLevelId);
        AssertEx.Equal(0, context.CouponRepository.Records[0].Status);
        AssertEx.Equal(null, context.CouponRepository.Records[0].OrderId);
        AssertEx.Equal(-30, context.PointRepository.Logs.Single().ChangeAmount);
        AssertCommitted(context);
    }

    private static async Task PaidRefundIsIdempotentAndPreventsShipmentAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            TestIds.Order,
            OrderStatus.Paid,
            "ORD-REFUND-PAID-001",
            pointsEarned: 30);

        await context.Service.DeductPointsForRefundAsync(TestIds.Customer, TestIds.Order, 50);
        await context.Service.DeductPointsForRefundAsync(TestIds.Customer, TestIds.Order, 50);

        AssertEx.Equal(
            OrderStatusCodes.Refunded,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(1, context.PointRepository.Logs.Count);
        AssertEx.Equal(-30, context.PointRepository.Logs[0].ChangeAmount);

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.TransitionOrderAsync(TestIds.Order, OrderStatus.Shipped));
        AssertEx.Equal(0, context.LogisticsService.ShippedOrderIds.Count);
    }

    private static async Task ShippedRefundDeductsAssetsAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            TestIds.Order,
            OrderStatus.Shipped,
            "ORD-REFUND-SHIPPED-001",
            pointsEarned: 30);

        await context.Service.DeductPointsForRefundAsync(TestIds.Customer, TestIds.Order, 30);

        AssertEx.Equal(
            OrderStatusCodes.Refunded,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertCommitted(context);
    }

    private static async Task RefundCustomerMismatchRollsBackAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.Points = 130;
        SeedOrder(
            context,
            TestIds.Order,
            OrderStatus.Paid,
            "ORD-REFUND-MISMATCH-001",
            pointsEarned: 30);

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.DeductPointsForRefundAsync("customer-2", TestIds.Order, 30));

        AssertEx.Equal(
            OrderStatusCodes.Paid,
            context.OrderRepository.Orders[0].OrderStatus);
        AssertEx.Equal(130, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0, context.PointRepository.Logs.Count);
        AssertRolledBack(context);
    }

    private static void SeedOrder(
        TestContext context,
        string orderId,
        OrderStatus status,
        string orderNo,
        int pointsEarned = 0)
    {
        context.OrderRepository.Orders.Add(new BizOrder
        {
            OrderId = orderId,
            OrderNo = orderNo,
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            ReceiverName = "默认收件人",
            ReceiverPhone = "13800138000",
            ReceiverProvince = "浙江省",
            ReceiverCity = "杭州市",
            ReceiverDistrict = "西湖区",
            ReceiverDetailAddress = "测试路1号",
            TotalAmount = 130m,
            DiscountAmount = 0m,
            FreightAmount = 0m,
            FinalAmount = 130m,
            PointsEarned = pointsEarned,
            OrderStatus = OrderStatusCodes.ToCode(status),
            CreatedAt = DateTime.Now.AddMinutes(-1)
        });
        context.OrderRepository.Details.AddRange(
        [
            new BizOrderDetail
            {
                OrderDetailId = $"{orderId}-detail-1",
                OrderId = orderId,
                ProductId = "P1",
                ProductName = "车厘子",
                Quantity = 1,
                UnitPrice = 50m,
                SubTotal = 50m,
                SupplierId = "SUP1"
            },
            new BizOrderDetail
            {
                OrderDetailId = $"{orderId}-detail-2",
                OrderId = orderId,
                ProductId = "P2",
                ProductName = "三文鱼",
                Quantity = 1,
                UnitPrice = 80m,
                SubTotal = 80m,
                SupplierId = "SUP2"
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
