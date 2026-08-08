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

    private static async Task SuccessfulOrderCommitsAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.UsableCoupon = new MktCouponUsage
        {
            RecordId = 7,
            CouponId = 3,
            CouponName = "满100减30",
            DiscountAmount = 30m
        };

        var result = await context.Service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 1,
            AddressId = 11,
            CouponRecordId = 7,
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
        request.CouponRecordId = 99;

        await AssertEx.ThrowsAsync<OrderBusinessException>(() =>
            context.Service.CreateOrderAsync(request));

        AssertRolledBackWithoutAssets(context);
    }

    private static async Task PointLogFailureRollsBackEverythingAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.UsableCoupon = new MktCouponUsage
        {
            RecordId = 7,
            CouponId = 3,
            CouponName = "满100减20",
            DiscountAmount = 20m
        };
        context.PointRepository.ThrowOnInsert = true;
        var request = CreateBasicRequest();
        request.CouponRecordId = 7;

        await AssertEx.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.CreateOrderAsync(request));

        AssertRolledBackWithoutAssets(context);
    }

    private static CreateOrderRequest CreateBasicRequest()
    {
        return new CreateOrderRequest
        {
            CustomerId = 1,
            AddressId = 11,
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
}
