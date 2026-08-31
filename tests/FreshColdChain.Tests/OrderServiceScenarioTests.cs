using FreshColdChain.Models;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class OrderServiceScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("正常下单提交订单、优惠券和双倍积分", SuccessfulOrderCommitsAsync),
            ("结算批次按团长原子拆单并分别计算运费", CheckoutBatchSplitsByPromoterAsync),
            ("结算采用C组团长商品售价", CheckoutUsesPromoterCatalogPriceAsync),
            ("团长无销售权时结算整体回滚", CheckoutRejectsUnauthorizedPromoterProductAsync),
            ("批次任一商品缺货时不创建任何子订单", CheckoutBatchStockFailureRollsBackAsync),
            ("模拟支付一次性支付整个结算批次", CheckoutBatchPaymentPaysAllOrdersAsync),
            ("结算积分最多抵扣10%且支付后按实付商品金额累计", CheckoutPointsRedeemAndEarnAsync),
            ("自动领取一张普通券并叠加一张特殊券", AutoClaimNormalAndSpecialCouponsAsync),
            ("支付流水失败时整个批次回滚", CheckoutBatchPaymentFailureRollsBackAsync),
            ("超过15分钟关闭整个结算批次并释放库存", ExpiredCheckoutBatchClosesAsync),
            ("库存不足时回滚且不创建订单", InsufficientStockRollsBackAsync),
            ("优惠券无效时回滚且不创建订单", InvalidCouponRollsBackAsync),
            ("积分流水失败时回滚全部已暂存变更", PointLogFailureRollsBackEverythingAsync)
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

        Console.WriteLine($"场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task CheckoutBatchPaymentPaysAllOrdersAsync()
    {
        var context = TestContext.Create();
        SeedPendingBatch(context, DateTime.Now.AddMinutes(15));

        var result = await context.Service.PayCheckoutBatchAsync(
            "batch-1",
            TestIds.Customer,
            new CheckoutBatchPaymentRequest { PaymentMethod = CheckoutPaymentMethods.WeChat });

        AssertEx.Equal(130m, result.PaidAmount);
        AssertEx.Equal(2, result.ChildOrderCount);
        AssertEx.True(context.OrderRepository.Orders.All(order => order.OrderStatus == OrderStatusCodes.Paid));
        AssertEx.Equal(2, context.PaymentRepository.Records.Count);
        AssertEx.Equal(1, context.PaymentRepository.Records.Select(record => record.TransactionNo).Distinct().Count());
    }

    private static async Task CheckoutPointsRedeemAndEarnAsync()
    {
        var context = TestContext.Create();
        var batch = await context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            PointsToUse = 100,
            Items =
            [
                new() { ProductId = "P1", PromoterId = "promoter-1", Quantity = 2 },
                new() { ProductId = "P2", PromoterId = "promoter-2", Quantity = 1 }
            ]
        });

        AssertEx.Equal(100, batch.PointsUsed);
        AssertEx.Equal(1m, batch.PointsDiscountAmount);
        AssertEx.Equal(159m, batch.FinalAmount);
        AssertEx.Equal(20m, batch.DiscountAmount);
        AssertEx.Equal(1, batch.AppliedCoupons.Count);
        AssertEx.True(batch.AppliedCoupons[0].WasAutoClaimed);
        AssertEx.Equal(0, context.CustomerRepository.Customer.Points);

        var payment = await context.Service.PayCheckoutBatchAsync(
            batch.CheckoutBatchId,
            TestIds.Customer,
            new CheckoutBatchPaymentRequest { PaymentMethod = CheckoutPaymentMethods.Alipay });

        AssertEx.Equal(159, payment.PointsEarned);
        AssertEx.Equal(159, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(159, context.OrderRepository.Orders.Sum(order => order.PointsEarned));
        AssertEx.Equal(2, context.PointRepository.Logs.Count);
    }

    private static async Task AutoClaimNormalAndSpecialCouponsAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.Coupons.Add(new MktCoupon
        {
            CouponId = "30000000000000000000000000000002",
            CouponName = "特殊叠加减5元",
            CouponType = "SPECIAL",
            MinOrderAmount = 100m,
            DiscountAmount = 5m,
            TotalQuantity = 10,
            RemainingQuantity = 3,
            StartTime = DateTime.Now.AddDays(-1),
            EndTime = DateTime.Now.AddDays(7),
            Status = 1
        });

        var batch = await context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            Items =
            [
                new() { ProductId = "P1", PromoterId = "promoter-1", Quantity = 2 },
                new() { ProductId = "P2", PromoterId = "promoter-2", Quantity = 1 }
            ]
        });

        AssertEx.Equal(25m, batch.DiscountAmount);
        AssertEx.Equal(155m, batch.FinalAmount);
        AssertEx.Equal(2, batch.AppliedCoupons.Count);
        AssertEx.Equal(1, batch.AppliedCoupons.Count(item => item.CouponType == "NORMAL"));
        AssertEx.Equal(1, batch.AppliedCoupons.Count(item => item.CouponType == "SPECIAL"));
        AssertEx.True(batch.AppliedCoupons.All(item => item.WasAutoClaimed));
        AssertEx.Equal(2, context.CouponRepository.Records.Count);
    }

    private static async Task CheckoutBatchPaymentFailureRollsBackAsync()
    {
        var context = TestContext.Create();
        SeedPendingBatch(context, DateTime.Now.AddMinutes(15));
        context.PaymentRepository.ThrowOnInsert = true;

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.PayCheckoutBatchAsync(
                "batch-1",
                TestIds.Customer,
                new CheckoutBatchPaymentRequest { PaymentMethod = CheckoutPaymentMethods.Alipay }));

        AssertEx.True(context.OrderRepository.Orders.All(order => order.OrderStatus == OrderStatusCodes.PendingPayment));
        AssertEx.Equal(0, context.PaymentRepository.Records.Count);
        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == true);
    }

    private static async Task ExpiredCheckoutBatchClosesAsync()
    {
        var context = TestContext.Create();
        SeedPendingBatch(context, DateTime.Now.AddSeconds(-1));

        var result = await context.Service.PayCheckoutBatchAsync(
            "batch-1",
            TestIds.Customer,
            new CheckoutBatchPaymentRequest
            {
                PaymentMethod = CheckoutPaymentMethods.BankCard,
                BankName = "中国工商银行"
            });

        AssertEx.True(result.IsExpired);
        AssertEx.True(context.OrderRepository.Orders.All(order => order.OrderStatus == OrderStatusCodes.Cancelled));
        AssertEx.Equal(2, context.InventoryService.ReleasedOrderIds.Count);
        AssertEx.Equal(0, context.PaymentRepository.Records.Count);
    }

    private static void SeedPendingBatch(TestContext context, DateTime expiresAt)
    {
        context.OrderRepository.Orders.AddRange(
        [
            new BizOrder
            {
                OrderId = "order-pay-1", OrderNo = "ORD-PAY-1", CustomerId = TestIds.Customer,
                CheckoutBatchId = "batch-1", PromoterId = "promoter-1", FinalAmount = 50m,
                OrderStatus = OrderStatusCodes.PendingPayment, PaymentExpiresAt = expiresAt
            },
            new BizOrder
            {
                OrderId = "order-pay-2", OrderNo = "ORD-PAY-2", CustomerId = TestIds.Customer,
                CheckoutBatchId = "batch-1", PromoterId = "promoter-2", FinalAmount = 80m,
                OrderStatus = OrderStatusCodes.PendingPayment, PaymentExpiresAt = expiresAt
            }
        ]);
        context.OrderRepository.Details.AddRange(
        [
            new BizOrderDetail { OrderDetailId = "detail-pay-1", OrderId = "order-pay-1", ProductId = "P1", ProductName = "车厘子", Quantity = 1, UnitPrice = 50m, SubTotal = 50m, SupplierId = "SUP1" },
            new BizOrderDetail { OrderDetailId = "detail-pay-2", OrderId = "order-pay-2", ProductId = "P2", ProductName = "三文鱼", Quantity = 1, UnitPrice = 80m, SubTotal = 80m, SupplierId = "SUP2" }
        ]);
    }

    private static async Task CheckoutBatchSplitsByPromoterAsync()
    {
        var context = TestContext.Create();
        context.LogisticsService.FreightAmount = 8m;
        var startedAt = DateTime.Now;

        var result = await context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            Items =
            [
                new() { ProductId = "P1", PromoterId = "promoter-1", Quantity = 2, ClientUnitPrice = 48m },
                new() { ProductId = "P2", PromoterId = "promoter-2", Quantity = 1, ClientUnitPrice = 80m }
            ]
        });

        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == true);
        AssertEx.Equal(2, result.Orders.Count);
        AssertEx.Equal(2, context.OrderRepository.Orders.Count);
        AssertEx.Equal(2, context.LogisticsService.FreightRequests.Count);
        AssertEx.Equal(180m, result.GoodsAmount);
        AssertEx.Equal(16m, result.FreightAmount);
        AssertEx.Equal(20m, result.DiscountAmount);
        AssertEx.Equal(176m, result.FinalAmount);
        AssertEx.Equal(1, result.AppliedCoupons.Count);
        AssertEx.True(result.AppliedCoupons[0].WasAutoClaimed);
        AssertEx.Equal(1, result.PriceChanges.Count);
        AssertEx.True(result.PaymentExpiresAt >= startedAt.AddMinutes(14));
        AssertEx.True(context.OrderRepository.Orders.All(order => order.CheckoutBatchId == result.CheckoutBatchId));
        AssertEx.True(context.OrderRepository.Orders.All(order => order.OrderStatus == OrderStatusCodes.PendingPayment));
        AssertEx.Equal(2, context.OrderRepository.Orders.Select(order => order.PromoterId).Distinct().Count());
    }

    private static async Task CheckoutUsesPromoterCatalogPriceAsync()
    {
        var context = TestContext.Create();
        context.PromoterCatalogService.PromoterPrices["P1"] = 45m;

        var result = await context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            Items =
            [
                new()
                {
                    ProductId = "P1",
                    PromoterId = "promoter-1",
                    Quantity = 2,
                    ClientUnitPrice = 50m
                }
            ]
        });

        AssertEx.Equal(90m, result.GoodsAmount);
        AssertEx.Equal(45m, context.OrderRepository.Details.Single().UnitPrice);
        AssertEx.Equal(1, result.PriceChanges.Count);
        AssertEx.Equal(45m, result.PriceChanges.Single().LatestPrice);
    }

    private static async Task CheckoutRejectsUnauthorizedPromoterProductAsync()
    {
        var context = TestContext.Create();
        context.PromoterCatalogService.DisallowedProductIds.Add("P1");

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
            {
                CustomerId = TestIds.Customer,
                AddressId = TestIds.Address1,
                Items =
                [
                    new()
                    {
                        ProductId = "P1",
                        PromoterId = "promoter-1",
                        Quantity = 1
                    }
                ]
            }));

        AssertRolledBackWithoutAssets(context);
    }

    private static async Task CheckoutBatchStockFailureRollsBackAsync()
    {
        var context = TestContext.Create();
        context.InventoryService.ExceptionToThrow = new OrderBusinessException("库存不足");

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.CreateCheckoutBatchAsync(new CreateOrderRequest
            {
                CustomerId = TestIds.Customer,
                AddressId = TestIds.Address1,
                Items =
                [
                    new() { ProductId = "P1", PromoterId = "promoter-1", Quantity = 1 },
                    new() { ProductId = "P2", PromoterId = "promoter-2", Quantity = 1 }
                ]
            }));

        AssertRolledBackWithoutAssets(context);
    }

    private static async Task SuccessfulOrderCommitsAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.UsableCoupon = new MktCouponUsage
        {
            RecordId = TestIds.Record,
            CouponId = TestIds.Coupon,
            CouponName = "满100减30",
            DiscountAmount = 30m
        };

        var result = await context.Service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            CouponRecordId = TestIds.Record,
            Items =
            [
                new() { ProductId = "P1", Quantity = 1 },
                new() { ProductId = "P1", Quantity = 1 },
                new() { ProductId = "P2", Quantity = 1 }
            ]
        });

        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == true);
        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == false);
        AssertEx.Equal(1, context.OrderRepository.Orders.Count);
        AssertEx.Equal(2, context.OrderRepository.Details.Count);
        AssertEx.Equal(2, context.InventoryService.LastItems.Count);
        AssertEx.Equal(2, context.InventoryService.LastItems.Single(item => item.ProductId == "P1").Quantity);
        AssertEx.Equal(180m, result.GoodsAmount);
        AssertEx.Equal(30m, result.DiscountAmount);
        AssertEx.Equal(150m, result.FinalAmount);
        AssertEx.Equal(30, result.PointsEarned);
        AssertEx.Equal(2, result.SupplierGroups.Count);
        AssertEx.Equal(130, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(150m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.True(context.CouponRepository.CouponUsed);
        AssertEx.Equal(1, context.PointRepository.Logs.Count);
        AssertEx.Equal(30, context.PointRepository.Logs[0].ChangeAmount);
        AssertEx.True(result.OrderNo.StartsWith("ORD", StringComparison.Ordinal));
        AssertEx.Equal(31, result.OrderNo.Length);
    }

    private static async Task InsufficientStockRollsBackAsync()
    {
        var context = TestContext.Create();
        context.InventoryService.ExceptionToThrow =
            new OrderBusinessException("库存不足");

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.CreateOrderAsync(CreateBasicRequest()));

        AssertRolledBackWithoutAssets(context);
    }

    private static async Task InvalidCouponRollsBackAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.UsableCoupon = null;
        var request = CreateBasicRequest();
        request.CouponRecordId = "missing-record";

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.CreateOrderAsync(request));

        AssertRolledBackWithoutAssets(context);
    }

    private static async Task PointLogFailureRollsBackEverythingAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.UsableCoupon = new MktCouponUsage
        {
            RecordId = TestIds.Record,
            CouponId = TestIds.Coupon,
            CouponName = "满100减20",
            DiscountAmount = 20m
        };
        context.PointRepository.ThrowOnInsert = true;
        var request = CreateBasicRequest();
        request.CouponRecordId = TestIds.Record;

        await AssertEx.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.CreateOrderAsync(request));

        AssertRolledBackWithoutAssets(context);
    }

    private static CreateOrderRequest CreateBasicRequest()
    {
        return new CreateOrderRequest
        {
            CustomerId = TestIds.Customer,
            AddressId = TestIds.Address1,
            Items = [new() { ProductId = "P1", Quantity = 2 }]
        };
    }

    private static void AssertRolledBackWithoutAssets(TestContext context)
    {
        AssertEx.True(context.TransactionManager.LastTransaction?.Committed == false);
        AssertEx.True(context.TransactionManager.LastTransaction?.RolledBack == true);
        AssertEx.Equal(0, context.OrderRepository.Orders.Count);
        AssertEx.Equal(0, context.OrderRepository.Details.Count);
        AssertEx.Equal(100, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(0m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.True(!context.CouponRepository.CouponUsed);
        AssertEx.Equal(0, context.PointRepository.Logs.Count);
    }
}

internal static class AssertEx
{
    public static void True(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("断言失败：条件应为 true");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"断言失败：期望 {expected}，实际 {actual}");
        }
    }

    public static async Task ThrowsAsync<TException>(Func<Task> operation)
        where TException : Exception
    {
        try
        {
            await operation();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"断言失败：期望抛出 {typeof(TException).Name}");
    }

    public static async Task<TException> ThrowsAndReturnAsync<TException>(
        Func<Task> operation)
        where TException : Exception
    {
        try
        {
            await operation();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(
            $"断言失败：期望抛出 {typeof(TException).Name}");
    }
}
