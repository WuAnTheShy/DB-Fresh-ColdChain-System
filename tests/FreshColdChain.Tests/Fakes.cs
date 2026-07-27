using System.Data;
using System.Diagnostics.CodeAnalysis;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;

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
        TransactionManager = new FakeTransactionManager();
        Service = new OrderService(
            OrderRepository,
            CustomerRepository,
            CouponRepository,
            PointRepository,
            InventoryService,
            TransactionManager);
    }

    public FakeOrderRepository OrderRepository { get; }
    public FakeCustomerRepository CustomerRepository { get; }
    public FakeCouponRepository CouponRepository { get; }
    public FakePointRepository PointRepository { get; }
    public FakeInventoryService InventoryService { get; }
    public FakeTransactionManager TransactionManager { get; }
    public OrderService Service { get; }

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

    public Task UpdateStatusAsync(
        int orderId,
        int status,
        IDbTransaction? transaction = null)
    {
        Stage(transaction, () =>
        {
            var order = Orders.Single(item => item.OrderId == orderId);
            order.OrderStatus = status;
        });
        return Task.CompletedTask;
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
        MemberLevelId = 2,
        Points = 100,
        TotalSpent = 0m
    };

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

    public Task<bool> AddressBelongsToCustomerAsync(
        int addressId,
        int customerId,
        IDbTransaction transaction)
    {
        return Task.FromResult(addressId == 11 && customerId == Customer.CustomerId);
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

    public Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(new List<CrmUserAddress>());
    }

    private static CrmCustomer CloneCustomer(CrmCustomer customer)
    {
        return new CrmCustomer
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.CustomerName,
            Phone = customer.Phone,
            Email = customer.Email,
            PasswordHash = customer.PasswordHash,
            PromoterId = customer.PromoterId,
            MemberLevelId = customer.MemberLevelId,
            TotalSpent = customer.TotalSpent,
            Points = customer.Points,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
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

    public Task<List<MktCouponRecord>> GetUserCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(new List<MktCouponRecord>());
    }

    public Task<MktCoupon?> GetCouponTemplateAsync(
        int couponId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult<MktCoupon?>(null);
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

    public Task<bool> DecrementCouponStockAsync(
        int couponId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult(true);
    }
}

internal sealed class FakePointRepository : IPointRepository
{
    public List<CrmPointLog> Logs { get; } = [];
    public bool ThrowOnInsert { get; set; }

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
        return Task.FromResult(new List<CrmMemberLevel>());
    }

    public Task<CrmMemberLevel?> GetLevelByIdAsync(
        int memberLevelId,
        IDbTransaction? transaction = null)
    {
        return Task.FromResult<CrmMemberLevel?>(new CrmMemberLevel
        {
            MemberLevelId = memberLevelId,
            LevelName = "双倍积分会员",
            MinSpent = 0m,
            DiscountRate = 1m,
            PointsMultiplier = 2
        });
    }
}

internal sealed class FakeInventoryService : IInventoryService
{
    public Exception? ExceptionToThrow { get; set; }
    public IReadOnlyList<InventoryReservationItem> LastItems { get; private set; } = [];

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
