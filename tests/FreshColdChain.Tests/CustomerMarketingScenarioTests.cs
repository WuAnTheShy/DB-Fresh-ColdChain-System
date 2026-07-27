using FreshColdChain.Models;
using FreshColdChain.Services;

namespace FreshColdChain.Tests;

internal static class CustomerMarketingScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("新增消费者使用密码哈希并初始化基础等级", CustomerCreationHashesPasswordAsync),
            ("消费者资料只更新允许维护的字段", ProfileUpdateCommitsAsync),
            ("第一条地址自动成为默认地址", FirstAddressBecomesDefaultAsync),
            ("编辑唯一地址时保持默认地址不变量", EditingOnlyAddressKeepsDefaultAsync),
            ("切换默认地址后仍只有一个默认地址", SwitchingDefaultKeepsInvariantAsync),
            ("删除默认地址时自动顺延", DeletingDefaultPromotesFallbackAsync),
            ("累计消费跨门槛后自动持久化会员等级", OrderAutomaticallyUpgradesMemberAsync),
            ("领券同时扣库存并产生可用券", CouponClaimCommitsAssetsAsync),
            ("重复领券回滚且不扣库存", DuplicateCouponClaimRollsBackAsync),
            ("券库存不足时不创建用户券", OutOfStockCouponRollsBackAsync)
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
            $"客户营销场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task CustomerCreationHashesPasswordAsync()
    {
        var context = TestContext.Create();

        var customerId = await context.CustomerService.CreateCustomerAsync(
            new CustomerCreateRequest
            {
                CustomerName = " 新消费者 ",
                Phone = "13600136000",
                Email = "new@example.com",
                Password = "SafePass123!",
                ConfirmPassword = "SafePass123!"
            });

        var customer = context.CustomerRepository.CreatedCustomers.Single();
        AssertEx.Equal(2, customerId);
        AssertEx.Equal("新消费者", customer.CustomerName);
        AssertEx.True(customer.PasswordHash != "SafePass123!");
        AssertEx.True(customer.PasswordHash.Length > 20);
        AssertEx.Equal(1, customer.MemberLevelId);
        AssertEx.Equal(0, customer.Points);
        AssertEx.Equal(0m, customer.TotalSpent);
        AssertCommitted(context);
    }

    private static async Task ProfileUpdateCommitsAsync()
    {
        var context = TestContext.Create();
        var originalPoints = context.CustomerRepository.Customer.Points;
        var originalSpent = context.CustomerRepository.Customer.TotalSpent;

        await context.CustomerService.UpdateProfileAsync(new CustomerProfileUpdateRequest
        {
            CustomerId = 1,
            CustomerName = "  更新后的消费者  ",
            Phone = "13900139000",
            Email = "  customer@example.com  "
        });

        AssertEx.Equal("更新后的消费者", context.CustomerRepository.Customer.CustomerName);
        AssertEx.Equal("13900139000", context.CustomerRepository.Customer.Phone);
        AssertEx.Equal("customer@example.com", context.CustomerRepository.Customer.Email);
        AssertEx.Equal(originalPoints, context.CustomerRepository.Customer.Points);
        AssertEx.Equal(originalSpent, context.CustomerRepository.Customer.TotalSpent);
        AssertCommitted(context);
    }

    private static async Task FirstAddressBecomesDefaultAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Addresses.Clear();

        var addressId = await context.CustomerService.CreateAddressAsync(
            CreateAddressRequest(isDefault: false));

        AssertEx.True(addressId > 0);
        AssertEx.Equal(1, context.CustomerRepository.Addresses.Count);
        AssertEx.Equal(1, context.CustomerRepository.Addresses[0].IsDefault);
        AssertCommitted(context);
    }

    private static async Task SwitchingDefaultKeepsInvariantAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Addresses.Add(CreateStoredAddress(12, isDefault: false));

        await context.CustomerService.SetDefaultAddressAsync(1, 12);

        AssertEx.Equal(
            1,
            context.CustomerRepository.Addresses.Count(address => address.IsDefault == 1));
        AssertEx.Equal(
            12,
            context.CustomerRepository.Addresses.Single(
                address => address.IsDefault == 1).AddressId);
        AssertCommitted(context);
    }

    private static async Task EditingOnlyAddressKeepsDefaultAsync()
    {
        var context = TestContext.Create();

        await context.CustomerService.UpdateAddressAsync(new AddressUpsertRequest
        {
            AddressId = 11,
            CustomerId = 1,
            ReceiverName = "编辑后收件人",
            Phone = "13500135000",
            Province = "浙江省",
            City = "杭州市",
            District = "拱墅区",
            DetailAddress = "湖墅南路1号",
            IsDefault = false
        });

        var address = context.CustomerRepository.Addresses.Single();
        AssertEx.Equal("编辑后收件人", address.ReceiverName);
        AssertEx.Equal("拱墅区", address.District);
        AssertEx.Equal(1, address.IsDefault);
        AssertCommitted(context);
    }

    private static async Task DeletingDefaultPromotesFallbackAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Addresses.Add(CreateStoredAddress(12, isDefault: false));

        await context.CustomerService.DeleteAddressAsync(1, 11);

        AssertEx.Equal(1, context.CustomerRepository.Addresses.Count);
        AssertEx.Equal(12, context.CustomerRepository.Addresses[0].AddressId);
        AssertEx.Equal(1, context.CustomerRepository.Addresses[0].IsDefault);
        AssertCommitted(context);
    }

    private static async Task OrderAutomaticallyUpgradesMemberAsync()
    {
        var context = TestContext.Create();
        context.CustomerRepository.Customer.MemberLevelId = 1;
        context.CustomerRepository.Customer.TotalSpent = 90m;

        var result = await context.Service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 1,
            AddressId = 11,
            Items = [new() { ProductId = 1, Quantity = 1 }]
        });

        AssertEx.Equal(50m, result.FinalAmount);
        AssertEx.Equal(5, result.PointsEarned);
        AssertEx.Equal(140m, context.CustomerRepository.Customer.TotalSpent);
        AssertEx.Equal(2, context.CustomerRepository.Customer.MemberLevelId);
        AssertCommitted(context);
    }

    private static async Task CouponClaimCommitsAssetsAsync()
    {
        var context = TestContext.Create();

        await context.CouponService.ClaimCouponAsync(1, 3);
        var center = await context.CouponService.GetCouponCenterAsync(1);

        AssertEx.Equal(1, context.CouponRepository.Coupons[0].RemainingQuantity);
        AssertEx.Equal(1, context.CouponRepository.Records.Count);
        AssertEx.Equal(1, center?.AvailableCoupons.Count ?? 0);
        AssertEx.Equal(1, center?.ClaimableCoupons[0].HasClaimed ?? 0);
        AssertCommitted(context);
    }

    private static async Task DuplicateCouponClaimRollsBackAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.Records.Add(new MktCouponRecord
        {
            RecordId = 1,
            CouponId = 3,
            CustomerId = 1,
            Status = 0,
            CreatedAt = DateTime.Now
        });

        await AssertEx.ThrowsAsync<GroupBBusinessException>(() =>
            context.CouponService.ClaimCouponAsync(1, 3));

        AssertEx.Equal(2, context.CouponRepository.Coupons[0].RemainingQuantity);
        AssertEx.Equal(1, context.CouponRepository.Records.Count);
        AssertRolledBack(context);
    }

    private static async Task OutOfStockCouponRollsBackAsync()
    {
        var context = TestContext.Create();
        context.CouponRepository.Coupons[0].RemainingQuantity = 0;

        await AssertEx.ThrowsAsync<GroupBBusinessException>(() =>
            context.CouponService.ClaimCouponAsync(1, 3));

        AssertEx.Equal(0, context.CouponRepository.Coupons[0].RemainingQuantity);
        AssertEx.Equal(0, context.CouponRepository.Records.Count);
        AssertRolledBack(context);
    }

    private static AddressUpsertRequest CreateAddressRequest(bool isDefault)
    {
        return new AddressUpsertRequest
        {
            CustomerId = 1,
            ReceiverName = "新收件人",
            Phone = "13800138000",
            Province = "浙江省",
            City = "杭州市",
            District = "余杭区",
            DetailAddress = "文一西路1号",
            IsDefault = isDefault
        };
    }

    private static CrmUserAddress CreateStoredAddress(int addressId, bool isDefault)
    {
        return new CrmUserAddress
        {
            AddressId = addressId,
            CustomerId = 1,
            ReceiverName = "备用收件人",
            Phone = "13700137000",
            Province = "浙江省",
            City = "杭州市",
            District = "滨江区",
            DetailAddress = "江南大道1号",
            IsDefault = isDefault ? 1 : 0,
            CreatedAt = DateTime.Now
        };
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
