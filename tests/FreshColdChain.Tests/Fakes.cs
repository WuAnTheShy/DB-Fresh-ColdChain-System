using System.Data;
using System.Diagnostics.CodeAnalysis;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Tests;

internal static class TestIds
{
    public const string Level1 = "level-1";
    public const string Level2 = "level-2";
    public const string Level3 = "level-3";
    public const string Customer = "customer-1";
    public const string Address1 = "address-11";
    public const string Address2 = "address-12";
    public const string Coupon = "coupon-3";
    public const string Record = "record-7";
    public const string Order = "order-1";
    public const string Order2 = "order-2";
}

internal sealed class TestContext
{
    private TestContext()
    {
        OrderRepository = new FakeOrderRepository();
        CustomerRepository = new FakeCustomerRepository();
        CouponRepository = new FakeCouponRepository();
        PointRepository = new FakePointRepository();
        InventoryService = new FakeInventoryService();
        LogisticsService = new FakeLogisticsService();
        CommissionService = new FakeCommissionService();
        PromoterService = new FakePromoterService();
        PaymentRepository = new FakePaymentRepository();
        TransactionManager = new FakeTransactionManager();
        AuthenticationState = new CustomerAuthenticationStateService();
        Service = new OrderService(
            OrderRepository,
            CustomerRepository,
            CouponRepository,
            PointRepository,
            InventoryService,
            LogisticsService,
            CommissionService,
            TransactionManager,
            PromoterService,
            PaymentRepository);
        CustomerService = new CustomerService(
            CustomerRepository,
            PointRepository,
            TransactionManager,
            new PasswordHasher<CrmCustomer>(),
            AuthenticationState);
        CouponService = new CouponService(
            CouponRepository,
            CustomerRepository,
            TransactionManager);
    }

    public FakeOrderRepository OrderRepository { get; }
    public FakeCustomerRepository CustomerRepository { get; }
    public FakeCouponRepository CouponRepository { get; }
    public FakePointRepository PointRepository { get; }
    public FakeInventoryService InventoryService { get; }
    public FakeLogisticsService LogisticsService { get; }
    public FakeCommissionService CommissionService { get; }
    public FakePromoterService PromoterService { get; }
    public FakePaymentRepository PaymentRepository { get; }
    public FakeTransactionManager TransactionManager { get; }
    public CustomerAuthenticationStateService AuthenticationState { get; }
    public OrderService Service { get; }
    public CustomerService CustomerService { get; }
    public CouponService CouponService { get; }

    public static TestContext Create()
    {
        return new TestContext();
    }
}

internal sealed class FakeOrderTransaction : IDbTransaction
{
    private readonly List<Action> _commitActions = [];

    public IDbConnection Connection { get; } = new FakeDbConnection();
    public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public void Stage(Action action)
    {
        _commitActions.Add(action);
    }

    public void Commit()
    {
        foreach (var action in _commitActions)
            action();
        Committed = true;
    }

    public void Rollback()
    {
        _commitActions.Clear();
        RolledBack = true;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeTransactionManager : IOrderTransactionManager
{
    public FakeOrderTransaction? LastTransaction { get; private set; }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<IDbTransaction, Task<TResult>> operation)
    {
        var transaction = new FakeOrderTransaction();
        LastTransaction = transaction;

        try
        {
            var result = await operation(transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task ExecuteAsync(Func<IDbTransaction, Task> operation)
    {
        await ExecuteAsync(async transaction =>
        {
            await operation(transaction);
            return true;
        });
    }
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    public List<BizOrder> Orders { get; } = [];
    public List<BizOrderDetail> Details { get; } = [];

    public Task<string> CreateOrderAsync(
        BizOrder order,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Orders.Add(order));
        return Task.FromResult(order.OrderId);
    }

    public Task<BizOrder?> GetByIdAsync(
        string orderId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Orders.SingleOrDefault(order => order.OrderId == orderId));
    }

    public Task<BizOrder?> GetByIdForUpdateAsync(
        string orderId,
        IDbTransaction transaction)
    {
        return GetByIdAsync(orderId, transaction);
    }

    public Task<List<BizOrder>> GetByCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction? transaction = null) => Task.FromResult(Orders
        .Where(order => order.CheckoutBatchId == checkoutBatchId && order.CustomerId == customerId)
        .OrderBy(order => order.OrderId)
        .ToList());

    public Task<List<BizOrder>> GetByCheckoutBatchForUpdateAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction transaction) => GetByCheckoutBatchAsync(checkoutBatchId, customerId, transaction);

    public Task<List<BizOrder>> GetOrdersForCommissionExpiryAsync(
        DateTime threshold,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Orders
            .Where(order =>
                (order.OrderStatus == OrderStatusCodes.Paid ||
                 order.OrderStatus == OrderStatusCodes.Shipped) &&
                (order.CommSettlementDate ?? order.CreatedAt) <= threshold)
            .Select(CloneOrder)
            .ToList());
    }

    public Task<int> CountOrdersAsync(
        OrderQueryRequest request,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(FilterOrders(request).Count());
    }

    public Task<List<OrderListItem>> GetOrdersAsync(
        OrderQueryRequest request,
        int offset,
        IDbTransaction? transaction = null)
    {
        var orders = FilterOrders(request)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderId)
            .Skip(offset)
            .Take(request.PageSize)
            .Select(order =>
            {
                var details = Details
                    .Where(detail => detail.OrderId == order.OrderId)
                    .ToList();
                return new OrderListItem
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,
                    CustomerId = order.CustomerId,
                    CustomerName = "测试消费者",
                    FinalAmount = order.FinalAmount,
                    OrderStatus = order.OrderStatus,
                    ItemCount = details.Count,
                    SupplierCount = details
                        .Where(detail => !string.IsNullOrWhiteSpace(detail.SupplierId))
                        .Select(detail => detail.SupplierId)
                        .Distinct()
                        .Count(),
                    CreatedAt = order.CreatedAt
                };
            })
            .ToList();
        return Task.FromResult(orders);
    }

    public Task<OrderDetailHeader?> GetDetailHeaderAsync(
        string orderId,
        IDbTransaction? transaction = null)
    {
        var order = Orders.SingleOrDefault(item => item.OrderId == orderId);
        return Task.FromResult(order == null
            ? null
            : new OrderDetailHeader
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                CustomerId = order.CustomerId,
                AddressId = order.AddressId,
                CustomerName = "测试消费者",
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ShippingAddress = order.ShippingAddress,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                FreightAmount = order.FreightAmount,
                FinalAmount = order.FinalAmount,
                PointsEarned = order.PointsEarned,
                PointsUsed = order.PointsUsed,
                PointsDiscountAmount = order.PointsDiscountAmount,
                OrderStatus = order.OrderStatus,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            });
    }

    public Task<List<BizOrderDetail>> GetDetailsAsync(
        string orderId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Details
            .Where(detail => detail.OrderId == orderId)
            .OrderBy(detail => detail.SupplierId)
            .ThenBy(detail => detail.OrderDetailId)
            .Select(CloneDetail)
            .ToList());
    }

    public Task<bool> TryUpdateStatusAsync(
        string orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction)
    {
        var order = Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (order == null ||
            order.OrderStatus != OrderStatusCodes.ToCode(expectedStatus))
            return Task.FromResult(false);

        Stage(transaction, () =>
        {
            order.OrderStatus = OrderStatusCodes.ToCode(targetStatus);
            order.UpdatedAt = DateTime.Now;
        });
        return Task.FromResult(true);
    }

    public Task<bool> TryUpdateCommissionSettlementAsync(
        string orderId,
        decimal? commBaseAmount,
        decimal? commBonusAmount,
        DateTime? commSettlementDate,
        IDbTransaction transaction)
    {
        var order = Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (order == null)
            return Task.FromResult(false);

        Stage(transaction, () =>
        {
            order.CommBaseAmount = commBaseAmount;
            order.CommBonusAmount = commBonusAmount;
            order.CommSettlementDate = commSettlementDate;
        });
        return Task.FromResult(true);
    }

    public Task InsertDetailsAsync(
        IEnumerable<BizOrderDetail> details,
        IDbTransaction? transaction = null)
    {
        var copies = details.Select(CloneDetail).ToList();
        Stage(transaction, () => Details.AddRange(copies));
        return Task.CompletedTask;
    }

    public Task<bool> TryConfirmDetailReceiptAsync(
        string orderDetailId,
        string orderId,
        IDbTransaction transaction)
    {
        var detail = Details.SingleOrDefault(item =>
            item.OrderDetailId == orderDetailId && item.OrderId == orderId);
        if (detail == null || detail.ReceiptStatus == "RECEIVED")
            return Task.FromResult(false);
        Stage(transaction, () =>
        {
            detail.ReceiptStatus = "RECEIVED";
            detail.ReceivedAt = DateTime.Now;
        });
        return Task.FromResult(true);
    }

    public Task<bool> HasUnreceivedDetailsExceptAsync(
        string orderId,
        string excludedOrderDetailId,
        IDbTransaction transaction) => Task.FromResult(Details.Any(detail =>
            detail.OrderId == orderId &&
            detail.OrderDetailId != excludedOrderDetailId &&
            detail.ReceiptStatus != "RECEIVED"));

    public Task<bool> UpdatePointsEarnedAsync(
        string orderId,
        int pointsEarned,
        IDbTransaction transaction)
    {
        var order = Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (order == null) return Task.FromResult(false);
        Stage(transaction, () => order.PointsEarned = pointsEarned);
        return Task.FromResult(true);
    }

    private static BizOrderDetail CloneDetail(BizOrderDetail detail)
    {
        return new BizOrderDetail
        {
            OrderDetailId = detail.OrderDetailId,
            OrderId = detail.OrderId,
            ProductId = detail.ProductId,
            ProductName = detail.ProductName,
            Quantity = detail.Quantity,
            UnitPrice = detail.UnitPrice,
            SubTotal = detail.SubTotal,
            SupplierId = detail.SupplierId,
            ReceiptStatus = detail.ReceiptStatus,
            ReceivedAt = detail.ReceivedAt
        };
    }

    private static BizOrder CloneOrder(BizOrder order)
    {
        return new BizOrder
        {
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            CustomerId = order.CustomerId,
            CheckoutBatchId = order.CheckoutBatchId,
            PromoterId = order.PromoterId,
            AddressId = order.AddressId,
            ReceiverName = order.ReceiverName,
            ReceiverPhone = order.ReceiverPhone,
            ShippingAddress = order.ShippingAddress,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            FreightAmount = order.FreightAmount,
            FinalAmount = order.FinalAmount,
            CommBaseAmount = order.CommBaseAmount,
            CommBonusAmount = order.CommBonusAmount,
            CommSettlementDate = order.CommSettlementDate,
            PointsEarned = order.PointsEarned,
            PointsUsed = order.PointsUsed,
            PointsDiscountAmount = order.PointsDiscountAmount,
            OrderStatus = order.OrderStatus,
            PaymentExpiresAt = order.PaymentExpiresAt,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    private IEnumerable<BizOrder> FilterOrders(OrderQueryRequest request)
    {
        return Orders.Where(order =>
            (request.CustomerId == null ||
             order.CustomerId == request.CustomerId) &&
            (!request.Status.HasValue ||
             order.OrderStatus == OrderStatusCodes.ToCode(request.Status.Value)) &&
            (request.Keyword == null ||
             order.OrderNo.Contains(
                 request.Keyword,
                 StringComparison.OrdinalIgnoreCase) ||
             "测试消费者".Contains(
                 request.Keyword,
                 StringComparison.OrdinalIgnoreCase)));
    }

    private static void Stage(IDbTransaction? transaction, Action action)
    {
        if (transaction is FakeOrderTransaction fakeTransaction)
            fakeTransaction.Stage(action);
        else
            action();
    }
}

internal sealed class FakeCustomerRepository : ICustomerRepository
{
    public CrmCustomer Customer { get; } = new()
    {
        CustomerId = TestIds.Customer,
        CustomerName = "测试消费者",
        OpenId = "openid-1",
        MemberLevelId = TestIds.Level2,
        Points = 100,
        TotalSpent = 0m,
        GrowthValue = 0
    };
    public List<CrmUserAddress> Addresses { get; } =
    [
        new()
        {
            AddressId = TestIds.Address1,
            CustomerId = TestIds.Customer,
            ReceiverName = "默认收件人",
            Phone = "13800138000",
            Province = "浙江省",
            City = "杭州市",
            District = "西湖区",
            DetailAddress = "测试路1号",
            IsDefault = 1,
            CreatedAt = DateTime.Now
        }
    ];
    public List<CrmCustomer> CreatedCustomers { get; } = [];

    public Task<bool> PhoneExistsAsync(
        string phone,
        string? excludeCustomerId = null,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(
            (Customer.CustomerId != excludeCustomerId &&
             string.Equals(Customer.Phone, phone, StringComparison.Ordinal)) ||
            CreatedCustomers.Any(item =>
                item.CustomerId != excludeCustomerId &&
                string.Equals(item.Phone, phone, StringComparison.Ordinal)));
    }

    public Task<string> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null)
    {
        var copy = CloneCustomer(customer);
        Stage(transaction, () => CreatedCustomers.Add(copy));
        return Task.FromResult(customer.CustomerId);
    }

    public Task<CrmCustomer?> GetByIdAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(customerId == Customer.CustomerId
            ? CloneCustomer(Customer)
            : null);
    }

    public Task<CrmCustomer?> GetByPhoneAsync(
        string phone,
        IDbTransaction? transaction = null)
    {
        var customer = new[] { Customer }
            .Concat(CreatedCustomers)
            .SingleOrDefault(item =>
                string.Equals(item.Phone, phone, StringComparison.Ordinal));
        return Task.FromResult(customer == null ? null : CloneCustomer(customer));
    }

    public Task<CrmCustomer?> GetByPhoneForUpdateAsync(
        string phone,
        IDbTransaction transaction)
    {
        return GetByPhoneAsync(phone, transaction);
    }

    public Task<bool> UpdatePasswordHashAsync(
        string customerId,
        string passwordHash,
        IDbTransaction transaction)
    {
        var customer = new[] { Customer }
            .Concat(CreatedCustomers)
            .SingleOrDefault(item => item.CustomerId == customerId);
        if (customer == null) return Task.FromResult(false);

        Stage(transaction, () =>
        {
            customer.PasswordHash = passwordHash;
            customer.UpdatedAt = DateTime.Now;
        });
        return Task.FromResult(true);
    }

    public Task<CrmCustomer?> GetByIdForUpdateAsync(
        string customerId,
        IDbTransaction transaction)
    {
        return GetByIdAsync(customerId, transaction);
    }

    public Task<List<CustomerAccount>> FindCustomerAccountsAsync(
        string? customerId = null,
        string? openId = null,
        string? phone = null,
        string? boundPromoterId = null,
        IDbTransaction? transaction = null)
    {
        var customers = new List<CrmCustomer> { Customer };
        customers.AddRange(CreatedCustomers);

        var result = customers.Where(item =>
            (customerId == null || item.CustomerId.ToString() == customerId) &&
            (openId == null || string.Equals(item.OpenId, openId, StringComparison.Ordinal)) &&
            (phone == null || string.Equals(item.Phone, phone, StringComparison.Ordinal)) &&
            (boundPromoterId == null || item.PromoterId == boundPromoterId))
            .Select(item => new CustomerAccount
            {
                CustomerID = item.CustomerId.ToString(),
                OpenID = item.OpenId,
                Phone = item.Phone,
                PointsBalance = item.Points,
                GrowthValue = item.GrowthValue,
                BoundPromoterID = item.PromoterId,
                BindExpireTime = item.BindExpireTime
            })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> UpdateBindingAsync(
        string customerId,
        string? boundPromoterId,
        DateTime? bindExpireTime,
        int? growthValue = null,
        IDbTransaction? transaction = null)
    {
        if (customerId != Customer.CustomerId)
            return Task.FromResult(false);

        Stage(transaction, () =>
        {
            Customer.PromoterId = boundPromoterId;
            Customer.BindExpireTime = bindExpireTime;
            if (growthValue.HasValue)
                Customer.GrowthValue = growthValue.Value;
        });
        return Task.FromResult(true);
    }

    public Task<List<CrmCustomer>> GetCustomersWithExpiredBindingsAsync(
        DateTime now,
        IDbTransaction? transaction = null)
    {
        var list = new List<CrmCustomer> { Customer }
            .Concat(CreatedCustomers)
            .Where(item => item.BindExpireTime.HasValue && item.BindExpireTime <= now)
            .Select(CloneCustomer)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<bool> UpdateProfileAsync(
        CustomerProfileUpdateRequest request,
        IDbTransaction? transaction = null)
    {
        if (request.CustomerId != Customer.CustomerId)
            return Task.FromResult(false);

        Stage(transaction, () =>
        {
            Customer.CustomerName = request.CustomerName;
            Customer.Phone = request.Phone;
            Customer.Email = request.Email;
        });
        return Task.FromResult(true);
    }

    public Task UpdatePointsAsync(
        string customerId,
        int newPoints,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.Points = newPoints);
        return Task.CompletedTask;
    }

    public Task UpdateTotalSpentAsync(
        string customerId,
        decimal addAmount,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.TotalSpent += addAmount);
        return Task.CompletedTask;
    }

    public Task<bool> TrySubtractTotalSpentAsync(
        string customerId,
        decimal amount,
        IDbTransaction transaction)
    {
        if (customerId != Customer.CustomerId ||
            Customer.TotalSpent < amount)
        {
            return Task.FromResult(false);
        }

        Stage(transaction, () => Customer.TotalSpent -= amount);
        return Task.FromResult(true);
    }

    public Task UpdateMemberLevelAsync(
        string customerId,
        string memberLevelId,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.MemberLevelId = memberLevelId);
        return Task.CompletedTask;
    }

    public Task<List<CrmUserAddress>> GetAddressesAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Addresses
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.AddressId)
            .Select(CloneAddress)
            .ToList());
    }

    public Task<CrmUserAddress?> GetAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null)
    {
        var address = Addresses.SingleOrDefault(item =>
            item.CustomerId == customerId &&
            item.AddressId == addressId);
        return Task.FromResult(address == null ? null : CloneAddress(address));
    }

    public Task<string> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        var copy = CloneAddress(address);
        Stage(transaction, () => Addresses.Add(copy));
        return Task.FromResult(address.AddressId);
    }

    public Task<bool> UpdateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        if (!Addresses.Any(item =>
            item.CustomerId == address.CustomerId &&
            item.AddressId == address.AddressId))
        {
            return Task.FromResult(false);
        }

        var copy = CloneAddress(address);
        Stage(transaction, () =>
        {
            var index = Addresses.FindIndex(item =>
                item.CustomerId == copy.CustomerId &&
                item.AddressId == copy.AddressId);
            Addresses[index] = copy;
        });
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null)
    {
        if (!Addresses.Any(item =>
            item.CustomerId == customerId &&
            item.AddressId == addressId))
        {
            return Task.FromResult(false);
        }

        Stage(transaction, () => Addresses.RemoveAll(item =>
            item.CustomerId == customerId &&
            item.AddressId == addressId));
        return Task.FromResult(true);
    }

    public Task ClearDefaultAddressesAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () =>
        {
            foreach (var address in Addresses.Where(
                item => item.CustomerId == customerId))
            {
                address.IsDefault = 0;
            }
        });
        return Task.CompletedTask;
    }

    public Task<bool> SetDefaultAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null)
    {
        if (!Addresses.Any(item =>
            item.CustomerId == customerId &&
            item.AddressId == addressId))
        {
            return Task.FromResult(false);
        }

        Stage(transaction, () =>
        {
            Addresses.Single(item =>
                item.CustomerId == customerId &&
                item.AddressId == addressId).IsDefault = 1;
        });
        return Task.FromResult(true);
    }

    private static CrmCustomer CloneCustomer(CrmCustomer customer)
    {
        return new CrmCustomer
        {
            CustomerId = customer.CustomerId,
            OpenId = customer.OpenId,
            CustomerName = customer.CustomerName,
            Phone = customer.Phone,
            Email = customer.Email,
            PasswordHash = customer.PasswordHash,
            PromoterId = customer.PromoterId,
            MemberLevelId = customer.MemberLevelId,
            TotalSpent = customer.TotalSpent,
            Points = customer.Points,
            GrowthValue = customer.GrowthValue,
            BindExpireTime = customer.BindExpireTime,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };
    }

    private static CrmUserAddress CloneAddress(CrmUserAddress address)
    {
        return new CrmUserAddress
        {
            AddressId = address.AddressId,
            CustomerId = address.CustomerId,
            ReceiverName = address.ReceiverName,
            Phone = address.Phone,
            Province = address.Province,
            City = address.City,
            District = address.District,
            DetailAddress = address.DetailAddress,
            IsDefault = address.IsDefault,
            CreatedAt = address.CreatedAt
        };
    }

    private static void Stage(IDbTransaction? transaction, Action action)
    {
        if (transaction is FakeOrderTransaction fakeTransaction)
            fakeTransaction.Stage(action);
        else
            action();
    }
}

internal sealed class FakeCouponRepository : ICouponRepository
{
    public MktCouponUsage? UsableCoupon { get; set; }
    public bool CouponUsed { get; private set; }
    public List<MktCoupon> Coupons { get; } =
    [
        new()
        {
            CouponId = TestIds.Coupon,
            CouponName = "满100减20",
            MinOrderAmount = 100m,
            DiscountAmount = 20m,
            TotalQuantity = 10,
            RemainingQuantity = 2,
            StartTime = DateTime.Now.AddDays(-1),
            EndTime = DateTime.Now.AddDays(7),
            Status = 1
        }
    ];
    public List<MktCouponRecord> Records { get; } = [];
    private Dictionary<string, MktCouponUsage> PendingUsages { get; } = [];

    public Task<List<MktCouponRecord>> GetUserCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Records
            .Where(record => record.CustomerId == customerId && record.Status == 0)
            .Select(CloneRecord)
            .ToList());
    }

    public Task<MktCoupon?> GetCouponTemplateAsync(
        string couponId,
        IDbTransaction? transaction = null)
    {
        var coupon = Coupons.SingleOrDefault(item => item.CouponId == couponId);
        return Task.FromResult(coupon == null ? null : CloneCoupon(coupon));
    }

    public Task<MktCoupon?> GetCouponTemplateForUpdateAsync(
        string couponId,
        IDbTransaction transaction)
    {
        return GetCouponTemplateAsync(couponId, transaction);
    }

    public Task<List<ClaimableCouponItem>> GetClaimableCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        var now = DateTime.Now;
        return Task.FromResult(Coupons
            .Where(coupon =>
                coupon.Status == 1 &&
                coupon.StartTime <= now &&
                coupon.EndTime >= now)
            .Select(coupon => new ClaimableCouponItem
            {
                CouponId = coupon.CouponId,
                CouponName = coupon.CouponName,
                CouponType = coupon.CouponType,
                MinOrderAmount = coupon.MinOrderAmount,
                DiscountAmount = coupon.DiscountAmount,
                RemainingQuantity = coupon.RemainingQuantity,
                EndTime = coupon.EndTime,
                HasClaimed = Records.Any(record =>
                    record.CustomerId == customerId &&
                    record.CouponId == coupon.CouponId) ? 1 : 0
            })
            .ToList());
    }

    public Task<List<AvailableCouponItem>> GetAvailableCouponsAsync(
        string customerId,
        IDbTransaction? transaction = null)
    {
        var now = DateTime.Now;
        var result =
            from record in Records
            join coupon in Coupons on record.CouponId equals coupon.CouponId
            where record.CustomerId == customerId
                && record.Status == 0
                && coupon.Status == 1
                && coupon.StartTime <= now
                && coupon.EndTime >= now
            select new AvailableCouponItem
            {
                RecordId = record.RecordId,
                CouponId = coupon.CouponId,
                CouponName = coupon.CouponName,
                CouponType = coupon.CouponType,
                MinOrderAmount = coupon.MinOrderAmount,
                DiscountAmount = coupon.DiscountAmount,
                EndTime = coupon.EndTime
            };
        return Task.FromResult(result.ToList());
    }

    public Task<bool> HasCustomerClaimedCouponAsync(
        string customerId,
        string couponId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Records.Any(record =>
            record.CustomerId == customerId &&
            record.CouponId == couponId));
    }

    public Task<string> CreateCouponRecordAsync(
        string recordId,
        string customerId,
        string couponId,
        IDbTransaction? transaction = null)
    {
        var coupon = Coupons.Single(item => item.CouponId == couponId);
        PendingUsages[recordId] = new MktCouponUsage
        {
            RecordId = recordId,
            CouponId = couponId,
            CouponName = coupon.CouponName,
            CouponType = coupon.CouponType,
            DiscountAmount = coupon.DiscountAmount
        };
        Stage(transaction, () => Records.Add(new MktCouponRecord
        {
            RecordId = recordId,
            CouponId = couponId,
            CustomerId = customerId,
            Status = 0,
            CreatedAt = DateTime.Now
        }));
        return Task.FromResult(recordId);
    }

    public Task<MktCouponUsage?> GetUsableCouponForUpdateAsync(
        string recordId,
        string customerId,
        decimal orderAmount,
        IDbTransaction transaction)
    {
        var coupon = UsableCoupon?.RecordId == recordId
            ? UsableCoupon
            : PendingUsages.GetValueOrDefault(recordId);
        return Task.FromResult(coupon);
    }

    public Task<bool> TryUseCouponAsync(
        string recordId,
        string customerId,
        string orderId,
        IDbTransaction transaction)
    {
        if (UsableCoupon?.RecordId != recordId && !PendingUsages.ContainsKey(recordId))
            return Task.FromResult(false);

        ((FakeOrderTransaction)transaction).Stage(() => CouponUsed = true);
        return Task.FromResult(true);
    }

    public Task<int> RestoreCouponForCancelledOrderAsync(
        string orderId,
        string customerId,
        IDbTransaction transaction)
    {
        var matchingRecords = Records
            .Where(record =>
                record.OrderId == orderId &&
                record.CustomerId == customerId &&
                record.Status == 1)
            .ToList();
        Stage(transaction, () =>
        {
            foreach (var record in matchingRecords)
            {
                record.Status = 0;
                record.OrderId = null;
                record.UsedAt = null;
            }
        });
        return Task.FromResult(matchingRecords.Count);
    }

    public Task<bool> DecrementCouponStockAsync(
        string couponId,
        IDbTransaction? transaction = null)
    {
        var coupon = Coupons.SingleOrDefault(item =>
            item.CouponId == couponId &&
            item.Status == 1 &&
            item.StartTime <= DateTime.Now &&
            item.EndTime >= DateTime.Now &&
            item.RemainingQuantity > 0);
        if (coupon == null)
            return Task.FromResult(false);

        Stage(transaction, () => coupon.RemainingQuantity--);
        return Task.FromResult(true);
    }

    private static MktCoupon CloneCoupon(MktCoupon coupon)
    {
        return new MktCoupon
        {
            CouponId = coupon.CouponId,
            CouponName = coupon.CouponName,
            CouponType = coupon.CouponType,
            MinOrderAmount = coupon.MinOrderAmount,
            DiscountAmount = coupon.DiscountAmount,
            TotalQuantity = coupon.TotalQuantity,
            RemainingQuantity = coupon.RemainingQuantity,
            StartTime = coupon.StartTime,
            EndTime = coupon.EndTime,
            Status = coupon.Status
        };
    }

    private static MktCouponRecord CloneRecord(MktCouponRecord record)
    {
        return new MktCouponRecord
        {
            RecordId = record.RecordId,
            CouponId = record.CouponId,
            CustomerId = record.CustomerId,
            OrderId = record.OrderId,
            Status = record.Status,
            UsedAt = record.UsedAt,
            CreatedAt = record.CreatedAt
        };
    }

    private static void Stage(IDbTransaction? transaction, Action action)
    {
        if (transaction is FakeOrderTransaction fakeTransaction)
            fakeTransaction.Stage(action);
        else
            action();
    }
}

internal sealed class FakePointRepository : IPointRepository
{
    public List<CrmPointLog> Logs { get; } = [];
    public bool ThrowOnInsert { get; set; }
    public List<CrmMemberLevel> Levels { get; } =
    [
        new()
        {
            MemberLevelId = TestIds.Level1,
            LevelName = "普通会员",
            MinSpent = 0m,
            DiscountRate = 1m,
            PointsMultiplier = 1
        },
        new()
        {
            MemberLevelId = TestIds.Level2,
            LevelName = "双倍积分会员",
            MinSpent = 100m,
            DiscountRate = 1m,
            PointsMultiplier = 2
        },
        new()
        {
            MemberLevelId = TestIds.Level3,
            LevelName = "三倍积分会员",
            MinSpent = 500m,
            DiscountRate = 1m,
            PointsMultiplier = 3
        }
    ];

    public Task InsertLogAsync(
        CrmPointLog log,
        IDbTransaction? transaction = null)
    {
        if (ThrowOnInsert)
            throw new InvalidOperationException("模拟积分流水写入失败");

        if (transaction is FakeOrderTransaction fakeTransaction)
            fakeTransaction.Stage(() => Logs.Add(log));
        else
            Logs.Add(log);
        return Task.CompletedTask;
    }

    public Task<bool> HasPointLogAsync(
        string customerId,
        string orderId,
        string changeType,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Logs.Any(log =>
            log.CustomerId == customerId &&
            log.OrderId == orderId &&
            string.Equals(
                log.ChangeType,
                changeType,
                StringComparison.Ordinal)));
    }

    public Task<List<CrmMemberLevel>> GetAllLevelsAsync(
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Levels
            .OrderBy(level => level.MinSpent)
            .Select(CloneLevel)
            .ToList());
    }

    public Task<CrmMemberLevel?> GetLevelByIdAsync(
        string memberLevelId,
        IDbTransaction? transaction = null)
    {
        var level = Levels.SingleOrDefault(
            item => item.MemberLevelId == memberLevelId);
        return Task.FromResult(level == null ? null : CloneLevel(level));
    }

    public Task<CrmMemberLevel?> GetLevelForSpentAsync(
        decimal totalSpent,
        IDbTransaction? transaction = null)
    {
        var level = Levels
            .Where(item => item.MinSpent <= totalSpent)
            .OrderByDescending(item => item.MinSpent)
            .FirstOrDefault();
        return Task.FromResult(level == null ? null : CloneLevel(level));
    }

    private static CrmMemberLevel CloneLevel(CrmMemberLevel level)
    {
        return new CrmMemberLevel
        {
            MemberLevelId = level.MemberLevelId,
            LevelName = level.LevelName,
            MinSpent = level.MinSpent,
            DiscountRate = level.DiscountRate,
            PointsMultiplier = level.PointsMultiplier
        };
    }
}

internal sealed class FakeInventoryService : IInventoryService
{
    public Exception? ExceptionToThrow { get; set; }
    public Exception? ReleaseExceptionToThrow { get; set; }
    public IReadOnlyList<InventoryReservationItem> LastItems { get; private set; } = [];
    public List<string> ReleasedOrderIds { get; } = [];

    public Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        LastItems = items;
        if (ExceptionToThrow != null)
            throw ExceptionToThrow;

        var snapshots = items.Select(item => item.ProductId switch
        {
            "P1" => new InventoryProductSnapshot
            {
                ProductId = "P1",
                ProductName = "车厘子",
                SupplierId = "SUP1",
                UnitPrice = 50m
            },
            "P2" => new InventoryProductSnapshot
            {
                ProductId = "P2",
                ProductName = "三文鱼",
                SupplierId = "SUP2",
                UnitPrice = 80m
            },
            _ => throw new OrderBusinessException("商品不存在")
        }).ToList();

        return Task.FromResult<IReadOnlyList<InventoryProductSnapshot>>(snapshots);
    }

    public Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ReleaseExceptionToThrow != null)
            throw ReleaseExceptionToThrow;

        ((FakeOrderTransaction)transaction).Stage(
            () => ReleasedOrderIds.Add(request.OrderId));
        return Task.CompletedTask;
    }
}

internal sealed class FakeLogisticsService : ILogisticsService
{
    public decimal FreightAmount { get; set; }
    public Exception? ShipmentExceptionToThrow { get; set; }
    public List<string> ShippedOrderIds { get; } = [];
    public FreightCalculationRequest? LastFreightRequest { get; private set; }
    public List<FreightCalculationRequest> FreightRequests { get; } = [];

    public Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        LastFreightRequest = request;
        FreightRequests.Add(request);
        return Task.FromResult(FreightAmount);
    }

    public Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ShipmentExceptionToThrow != null)
            throw ShipmentExceptionToThrow;

        ((FakeOrderTransaction)transaction).Stage(
            () => ShippedOrderIds.Add(request.OrderId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SupplierFulfillmentStatus> statuses = supplierIds
            .Select(supplierId => new SupplierFulfillmentStatus
            {
                SupplierId = supplierId,
                StatusName = "待发货",
                TrackingNo = $"TRACK-{orderId}-{supplierId}"
            })
            .ToList();
        return Task.FromResult(statuses);
    }
}

internal sealed class FakeCommissionService : ICommissionService
{
    public Exception? ExceptionToThrow { get; set; }
    public List<CommissionOrderRequest> CompletedOrders { get; } = [];

    public Task<CommissionResult> RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null)
            throw ExceptionToThrow;

        ((FakeOrderTransaction)transaction).Stage(
            () => CompletedOrders.Add(request));
        return Task.FromResult(new CommissionResult { IsSuccess = true });
    }

    public Task<Result> ActivatePromoterMoney(
        ActivateCommissionOrderRequest request,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Result { IsSuccess = true });
    }
}

internal sealed class FakePaymentRepository : IPaymentRepository
{
    public List<GroupC_FinPaymentRecord> Records { get; } = [];
    public bool ThrowOnInsert { get; set; }

    public Task GroupC_AddPaymentRecordAsync(
        GroupC_FinPaymentRecord finPaymentRecord,
        IDbTransaction? transaction = null)
    {
        if (ThrowOnInsert) throw new InvalidOperationException("支付流水写入失败");
        var copy = new GroupC_FinPaymentRecord
        {
            PayId = finPaymentRecord.PayId,
            OrderId = finPaymentRecord.OrderId,
            PayMethod = finPaymentRecord.PayMethod,
            TransactionNo = finPaymentRecord.TransactionNo,
            PayAmount = finPaymentRecord.PayAmount,
            Status = finPaymentRecord.Status,
            PayTime = finPaymentRecord.PayTime,
            Remark = finPaymentRecord.Remark
        };
        if (transaction is FakeOrderTransaction fakeTransaction)
            fakeTransaction.Stage(() => Records.Add(copy));
        else
            Records.Add(copy);
        return Task.CompletedTask;
    }

    public Task<List<GroupC_FinPaymentRecord>> SearchAsync(
        DateTime? startTime,
        DateTime? endTime,
        string? orderId,
        string? status,
        IDbTransaction? transaction = null) => Task.FromResult(Records.ToList());
}

internal sealed class FakePromoterService : IPromoterService
{
    public List<string> BoundPromoterIds { get; } = ["promoter-1", "promoter-2"];

    public Task<GroupC_PagedResult<GroupC_AvailablePromoterDto>> GetAvailablePromotersAsync(
        GroupC_AvailablePromoterQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GroupC_PagedResult<GroupC_AvailablePromoterDto>());

    public Task<GroupC_PromoterBasicInfoDto?> GetPromoterBasicInfoAsync(
        string promoterId,
        CancellationToken cancellationToken = default) => Task.FromResult<GroupC_PromoterBasicInfoDto?>(null);

    public Task<List<string>> GetActiveSupplierIdsAsync(string promoterId) => Task.FromResult(new List<string>());

    public Task<Dictionary<string, bool>> ValidateSuppliersAsync(string promoterId, List<string> supplierIds) =>
        Task.FromResult(supplierIds.ToDictionary(id => id, _ => true, StringComparer.Ordinal));

    public Task<Result> BindCustomerToPromoterAsync(
        string customerId,
        string promoterId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (!BoundPromoterIds.Contains(promoterId, StringComparer.Ordinal))
            BoundPromoterIds.Add(promoterId);
        return Task.FromResult(new Result { IsSuccess = true });
    }

    public Task<List<string>> GetBoundPromoterIdsAsync(string customerId) =>
        Task.FromResult(BoundPromoterIds.ToList());

    public Task<Result> UnbindCustomerFromPromoterAsync(
        string customerId,
        string promoterId,
        CancellationToken cancellationToken = default)
    {
        BoundPromoterIds.Remove(promoterId);
        return Task.FromResult(new Result { IsSuccess = true });
    }

    public Task<List<GroupC_CrmPCRelation>> GetBoundCustomersByPromoterAsync(string promoterId) =>
        Task.FromResult(new List<GroupC_CrmPCRelation>());
}

internal sealed class FakeDbConnection : IDbConnection
{
    [AllowNull]
    public string ConnectionString { get; set; } = string.Empty;
    public int ConnectionTimeout => 0;
    public string Database => "Fake";
    public ConnectionState State => ConnectionState.Open;

    public IDbTransaction BeginTransaction()
    {
        throw new NotSupportedException();
    }

    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
        throw new NotSupportedException();
    }

    public void ChangeDatabase(string databaseName)
    {
        throw new NotSupportedException();
    }

    public void Close()
    {
    }

    public IDbCommand CreateCommand()
    {
        throw new NotSupportedException();
    }

    public void Open()
    {
    }

    public void Dispose()
    {
    }
}
