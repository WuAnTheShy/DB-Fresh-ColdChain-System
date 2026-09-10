using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

// B 组消费者绑定和会员等级的周期维护服务。
public sealed class GroupBDailyMaintenanceService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPointRepository _pointRepository;
    private readonly IOrderTransactionManager _transactionManager;

    public GroupBDailyMaintenanceService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IPointRepository pointRepository,
        IOrderTransactionManager transactionManager)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _pointRepository = pointRepository;
        _transactionManager = transactionManager;
    }

    public async Task RunDailyChecksAsync(CancellationToken cancellationToken = default)
    {
        await RunBindingExpiryCheckAsync(cancellationToken);
        await RunMonthlyMemberLevelSettlementAsync(cancellationToken);
    }

    public async Task RunBindingExpiryCheckAsync(CancellationToken cancellationToken = default)
    {
        var expiredCustomers = await _customerRepository.GetCustomersWithExpiredBindingsAsync(DateTime.Now);
        foreach (var customer in expiredCustomers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _transactionManager.ExecuteAsync(async transaction =>
            {
                var lockedCustomer = await _customerRepository.GetByIdForUpdateAsync(customer.CustomerId, transaction);
                if (lockedCustomer?.BindExpireTime == null || lockedCustomer.BindExpireTime > DateTime.Now)
                    return;

                if (!await _customerRepository.UpdateBindingAsync(
                    lockedCustomer.CustomerId,
                    null,
                    null,
                    lockedCustomer.GrowthValue,
                    transaction))
                {
                    throw new GroupBBusinessException("消费者团长绑定清理失败");
                }
            });
        }
    }

    // 每月 1 日以前一月末为截止点，以已完结订单累计金额重新定级并留痕。
    public async Task RunMonthlyMemberLevelSettlementAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        if (today.Day != 1)
            return;

        var settlementMonth = new DateTime(today.Year, today.Month, 1);
        var customers = await _customerRepository.GetAllCustomersAsync();
        foreach (var customer in customers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _transactionManager.ExecuteAsync(async transaction =>
            {
                if (await _pointRepository.HasMemberLevelHistoryAsync(customer.CustomerId, settlementMonth, transaction))
                    return;
                var locked = await _customerRepository.GetByIdForUpdateAsync(customer.CustomerId, transaction);
                if (locked == null)
                    return;
                var spent = await _orderRepository.GetCompletedSpentBeforeAsync(customer.CustomerId, settlementMonth, transaction);
                var level = await _pointRepository.GetLevelForSpentAsync(spent, transaction);
                if (level == null)
                    throw new GroupBBusinessException("会员等级配置不完整");
                await _customerRepository.SetTotalSpentAsync(customer.CustomerId, spent, transaction);
                await _customerRepository.UpdateMemberLevelAsync(customer.CustomerId, level.MemberLevelId, transaction);
                await _pointRepository.InsertMemberLevelHistoryAsync(new CrmMemberLevelHistory
                {
                    HistoryId = GroupBIds.NewId(),
                    CustomerId = customer.CustomerId,
                    MemberLevelId = level.MemberLevelId,
                    QualifiedSpent = spent,
                    SettlementMonth = settlementMonth
                }, transaction);
            });
        }
    }
}

public sealed class GroupBDailyCheckHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GroupBDailyCheckHostedService> _logger;

    public GroupBDailyCheckHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<GroupBDailyCheckHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<GroupBDailyMaintenanceService>();
            await maintenance.RunDailyChecksAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "每日消费者绑定与会员定级巡检执行失败");
        }
    }
}

// 独立于每日巡检的短周期任务，关闭超时结算批次并自动确认已发货订单。
public sealed class GroupBCheckoutExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GroupBCheckoutExpiryHostedService> _logger;

    public GroupBCheckoutExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<GroupBCheckoutExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            await orderService.ExpirePendingCheckoutBatchesAsync(stoppingToken);
            await orderService.AutoConfirmShippedOrdersAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "超时结算批次关闭任务执行失败");
        }
    }
}
