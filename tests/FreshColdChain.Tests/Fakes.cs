using System.Data;
using System.Diagnostics.CodeAnalysis;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Tests;

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
        TransactionManager = new FakeTransactionManager();
        Service = new OrderService(
            OrderRepository,
            CustomerRepository,
            CouponRepository,
            PointRepository,
            InventoryService,
            LogisticsService,
            CommissionService,
            TransactionManager);
        CustomerService = new CustomerService(
            CustomerRepository,
            PointRepository,
            TransactionManager,
            new PasswordHasher<CrmCustomer>());
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
    public FakeTransactionManager TransactionManager { get; }
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

    public Task<int> CreateOrderAsync(
        BizOrder order,
        IDbTransaction? transaction = null)
    {
        var orderId = Orders.Count + 1;
        Stage(transaction, () =>
        {
            order.OrderId = orderId;
            Orders.Add(order);
        });
        return Task.FromResult(orderId);
    }

    public Task<BizOrder?> GetByIdAsync(
        int orderId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Orders.SingleOrDefault(order => order.OrderId == orderId));
    }

    public Task<BizOrder?> GetByIdForUpdateAsync(
        int orderId,
        IDbTransaction transaction)
    {
        return GetByIdAsync(orderId, transaction);
    }

    public Task<List<BizOrder>> GetOrdersForCommissionExpiryAsync(
        DateTime threshold,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Orders
            .Where(order =>
                (order.OrderStatus == (int)OrderStatus.Paid ||
                 order.OrderStatus == (int)OrderStatus.Shipped) &&
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
                        .Where(detail => detail.SupplierId.HasValue)
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
        int orderId,
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
                OrderStatus = order.OrderStatus,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            });
    }

    public Task<List<BizOrderDetail>> GetDetailsAsync(
        int orderId,
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
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction)
    {
        var order = Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (order == null || order.OrderStatus != (int)expectedStatus)
            return Task.FromResult(false);

        Stage(transaction, () =>
        {
            order.OrderStatus = (int)targetStatus;
            order.UpdatedAt = DateTime.Now;
        });
        return Task.FromResult(true);
    }

    public Task<bool> TryUpdateCommissionSettlementAsync(
        int orderId,
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
            SupplierId = detail.SupplierId
        };
    }

    private static BizOrder CloneOrder(BizOrder order)
    {
        return new BizOrder
        {
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            CustomerId = order.CustomerId,
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
            OrderStatus = order.OrderStatus,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    private IEnumerable<BizOrder> FilterOrders(OrderQueryRequest request)
    {
        return Orders.Where(order =>
            (!request.CustomerId.HasValue ||
             order.CustomerId == request.CustomerId.Value) &&
            (!request.Status.HasValue ||
             order.OrderStatus == (int)request.Status.Value) &&
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
        CustomerId = 1,
        CustomerName = "测试消费者",
        OpenId = "openid-1",
        MemberLevelId = 2,
        Points = 100,
        TotalSpent = 0m,
        GrowthValue = 0
    };
    public List<CrmUserAddress> Addresses { get; } =
    [
        new()
        {
            AddressId = 11,
            CustomerId = 1,
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
        int? excludeCustomerId = null,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(
            (Customer.CustomerId != excludeCustomerId &&
             string.Equals(Customer.Phone, phone, StringComparison.Ordinal)) ||
            CreatedCustomers.Any(item =>
                item.CustomerId != excludeCustomerId &&
                string.Equals(item.Phone, phone, StringComparison.Ordinal)));
    }

    public Task<int> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null)
    {
        var customerId = 2 + CreatedCustomers.Count;
        var copy = CloneCustomer(customer);
        copy.CustomerId = customerId;
        Stage(transaction, () => CreatedCustomers.Add(copy));
        return Task.FromResult(customerId);
    }

    public Task<CrmCustomer?> GetByIdAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(customerId == Customer.CustomerId
            ? CloneCustomer(Customer)
            : null);
    }

    public Task<CrmCustomer?> GetByIdForUpdateAsync(
        int customerId,
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
            (boundPromoterId == null || item.PromoterId?.ToString() == boundPromoterId))
            .Select(item => new CustomerAccount
            {
                CustomerID = item.CustomerId.ToString(),
                OpenID = item.OpenId,
                Phone = item.Phone,
                PointsBalance = item.Points,
                GrowthValue = item.GrowthValue,
                BoundPromoterID = item.PromoterId?.ToString(),
                BindExpireTime = item.BindExpireTime
            })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> UpdateBindingAsync(
        int customerId,
        int? boundPromoterId,
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
        int customerId,
        int newPoints,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.Points = newPoints);
        return Task.CompletedTask;
    }

    public Task UpdateTotalSpentAsync(
        int customerId,
        decimal addAmount,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.TotalSpent += addAmount);
        return Task.CompletedTask;
    }

    public Task<bool> TrySubtractTotalSpentAsync(
        int customerId,
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
        int customerId,
        int memberLevelId,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () => Customer.MemberLevelId = memberLevelId);
        return Task.CompletedTask;
    }

    public Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
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
        int customerId,
        int addressId,
        IDbTransaction? transaction = null)
    {
        var address = Addresses.SingleOrDefault(item =>
            item.CustomerId == customerId &&
            item.AddressId == addressId);
        return Task.FromResult(address == null ? null : CloneAddress(address));
    }

    public Task<int> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        var addressId = Addresses.Count == 0
            ? 1
            : Addresses.Max(item => item.AddressId) + 1;
        var copy = CloneAddress(address);
        copy.AddressId = addressId;
        Stage(transaction, () => Addresses.Add(copy));
        return Task.FromResult(addressId);
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
        int customerId,
        int addressId,
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
        int customerId,
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
        int customerId,
        int addressId,
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
            CouponId = 3,
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

    public Task<List<MktCouponRecord>> GetUserCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Records
            .Where(record => record.CustomerId == customerId && record.Status == 0)
            .Select(CloneRecord)
            .ToList());
    }

    public Task<MktCoupon?> GetCouponTemplateAsync(
        int couponId,
        IDbTransaction? transaction = null)
    {
        var coupon = Coupons.SingleOrDefault(item => item.CouponId == couponId);
        return Task.FromResult(coupon == null ? null : CloneCoupon(coupon));
    }

    public Task<MktCoupon?> GetCouponTemplateForUpdateAsync(
        int couponId,
        IDbTransaction transaction)
    {
        return GetCouponTemplateAsync(couponId, transaction);
    }

    public Task<List<ClaimableCouponItem>> GetClaimableCouponsAsync(
        int customerId,
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
        int customerId,
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
                MinOrderAmount = coupon.MinOrderAmount,
                DiscountAmount = coupon.DiscountAmount,
                EndTime = coupon.EndTime
            };
        return Task.FromResult(result.ToList());
    }

    public Task<bool> HasCustomerClaimedCouponAsync(
        int customerId,
        int couponId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Records.Any(record =>
            record.CustomerId == customerId &&
            record.CouponId == couponId));
    }

    public Task<int> CreateCouponRecordAsync(
        int customerId,
        int couponId,
        IDbTransaction transaction)
    {
        var recordId = Records.Count == 0
            ? 1
            : Records.Max(record => record.RecordId) + 1;
        ((FakeOrderTransaction)transaction).Stage(() => Records.Add(new MktCouponRecord
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
        int recordId,
        int customerId,
        decimal orderAmount,
        IDbTransaction transaction)
    {
        var coupon = UsableCoupon?.RecordId == recordId
            ? UsableCoupon
            : null;
        return Task.FromResult(coupon);
    }

    public Task<bool> TryUseCouponAsync(
        int recordId,
        int customerId,
        int orderId,
        IDbTransaction transaction)
    {
        if (UsableCoupon?.RecordId != recordId)
            return Task.FromResult(false);

        ((FakeOrderTransaction)transaction).Stage(() => CouponUsed = true);
        return Task.FromResult(true);
    }

    public Task<int> RestoreCouponForCancelledOrderAsync(
        int orderId,
        int customerId,
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
        int couponId,
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
            MemberLevelId = 1,
            LevelName = "普通会员",
            MinSpent = 0m,
            DiscountRate = 1m,
            PointsMultiplier = 1
        },
        new()
        {
            MemberLevelId = 2,
            LevelName = "双倍积分会员",
            MinSpent = 100m,
            DiscountRate = 1m,
            PointsMultiplier = 2
        },
        new()
        {
            MemberLevelId = 3,
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

    public Task<List<CrmMemberLevel>> GetAllLevelsAsync(
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(Levels
            .OrderBy(level => level.MinSpent)
            .Select(CloneLevel)
            .ToList());
    }

    public Task<CrmMemberLevel?> GetLevelByIdAsync(
        int memberLevelId,
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
    public List<int> ReleasedOrderIds { get; } = [];

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
            1 => new InventoryProductSnapshot
            {
                ProductId = 1,
                ProductName = "车厘子",
                SupplierId = 1,
                UnitPrice = 50m
            },
            2 => new InventoryProductSnapshot
            {
                ProductId = 2,
                ProductName = "三文鱼",
                SupplierId = 2,
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
    public List<int> ShippedOrderIds { get; } = [];
    public FreightCalculationRequest? LastFreightRequest { get; private set; }

    public Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        LastFreightRequest = request;
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
        int orderId,
        IReadOnlyList<int> supplierIds,
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

    public Task RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null)
            throw ExceptionToThrow;

        ((FakeOrderTransaction)transaction).Stage(
            () => CompletedOrders.Add(request));
        return Task.CompletedTask;
    }
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
