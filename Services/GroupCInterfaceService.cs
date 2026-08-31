using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services;

public sealed class GroupCInterfaceService : IGroupCInterface
{
    private static readonly TimeSpan RefundWindow = TimeSpan.FromDays(14);

    private readonly ICustomerRepository _customerRepository;
    private readonly IPromoterRepository _promoterRepository;
    private readonly ILogger<GroupCInterfaceService> _logger;

    public GroupCInterfaceService(
        ICustomerRepository customerRepository,
        IPromoterRepository promoterRepository,
        ILogger<GroupCInterfaceService> logger)
    {
        _customerRepository = customerRepository;
        _promoterRepository = promoterRepository;
        _logger = logger;
    }

    public async Task<CustomerAccount[]> FindCustomerAccountInfoAsync(
        string customerID,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FindCustomerAccountsAsync(customerId: customerID, cancellationToken: cancellationToken);
    }

    public async Task<CustomerAccount[]> FindCustomerAccountInfoByOpenIdAsync(
        string openID,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FindCustomerAccountsAsync(openId: openID, cancellationToken: cancellationToken);
    }

    public async Task<CustomerAccount[]> FindCustomerAccountInfoByPhoneAsync(
        string phone,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FindCustomerAccountsAsync(phone: phone, cancellationToken: cancellationToken);
    }

    public async Task<CustomerAccount[]> FindCustomerAccountInfoByBoundPromoterIdAsync(
        string boundPromoterID,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FindCustomerAccountsAsync(boundPromoterId: boundPromoterID, cancellationToken: cancellationToken);
    }

    public Task<bool> WriteTableChangeLogAsync(
        TableChangeLogInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input == null)
            return Task.FromResult(false);

        var hasAnyValue = !string.IsNullOrWhiteSpace(input.TableName)
            || !string.IsNullOrWhiteSpace(input.ActionType)
            || !string.IsNullOrWhiteSpace(input.OldValue)
            || !string.IsNullOrWhiteSpace(input.NewValue)
            || !string.IsNullOrWhiteSpace(input.OperatorType);
        if (!hasAnyValue)
            return Task.FromResult(false);

        _logger.LogInformation(
            "审计日志已接收: {TableName} {ActionType}",
            input.TableName,
            input.ActionType);
        return Task.FromResult(true);
    }

    public Task<bool> AuditOrderAsync(
        AuditOrderInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input == null || input.GoodsList.Count == 0)
            return Task.FromResult(false);

        var isValid = input.GoodsList.All(item => item.ProductID > 0 && item.Quantity > 0);
        return Task.FromResult(isValid);
    }

    public async Task<CommissionInfo> CommissionSettlementAsync(
        CommissionSettlementInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (!int.TryParse(input.PromoterID, out var promoterId) || promoterId <= 0)
        {
            return new CommissionInfo
            {
                CommBaseAmount = 0d,
                CommBonusAmount = 0d,
                CommSettlementDate = DateTime.Now
            };
        }

        var promoter = await _promoterRepository.GetByIdAsync(promoterId);
        var baseRate = promoter?.BaseCommissionRate > 0m
            ? promoter.BaseCommissionRate
            : 0.05m;

        var commBaseAmount = Math.Round(input.FinalAmount * (double)baseRate, 2);
        var commBonusAmount = input.GoodsAmount switch
        {
            >= 1000d => Math.Round(commBaseAmount * 0.20d, 2),
            >= 500d => Math.Round(commBaseAmount * 0.10d, 2),
            _ => 0d
        };

        return new CommissionInfo
        {
            CommBaseAmount = commBaseAmount,
            CommBonusAmount = commBonusAmount,
            CommSettlementDate = DateTime.Now
        };
    }

    public Task PaymentRecordReactionAsync(
        PaymentRecordReactionInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogInformation(
            "支付流水已接收: Order {OrderID}, Status {Status}, Method {PayMethod}",
            input.OrderID,
            input.Status,
            input.PayMethod);
        return Task.CompletedTask;
    }

    public async Task<bool> RefundRollbackMoneyAsync(
        RefundRollbackMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (input.Status is not ("Status_2" or "Status_3"))
            return false;
        if (!int.TryParse(input.PromoterID, out var promoterId) || promoterId <= 0)
            return false;

        var baseAmount = TryParseDecimal(input.CommBaseAmount);
        var bonusAmount = TryParseDecimal(input.CommBonusAmount);
        var salesAmount = (decimal)(input.GoodsAmount ?? 0d);

        return await _promoterRepository.TryRollbackCommissionAsync(
            promoterId,
            baseAmount,
            bonusAmount,
            salesAmount,
            transaction);
    }

    public async Task<PromBind> FindPromoterInfoAsync(
        string inviteCode,
        string promoterName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(inviteCode) || string.IsNullOrWhiteSpace(promoterName))
        {
            return new PromBind { IsValid = false };
        }

        var promoter = await _promoterRepository.FindByInviteCodeAndNameAsync(
            inviteCode.Trim(),
            promoterName.Trim());
        if (promoter == null)
            return new PromBind { IsValid = false };

        var avatar = string.IsNullOrWhiteSpace(promoter.Avatar) ? null : promoter.Avatar.Trim();

        return new PromBind
        {
            IsValid = true,
            BoundPromoterID = promoter.PromoterId.ToString(),
            BindExpireTime = DateTime.Now.AddYears(3),
            Avatar = avatar,
            AvatarUrl = string.IsNullOrWhiteSpace(avatar)
                ? null
                : $"/images/avatars/{avatar}.png"
        };
    }

    public async Task ActivatePromoterMoneyAsync(
        ActivatePromoterMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (!int.TryParse(input.PromoterID, out var promoterId) || promoterId <= 0)
            return;

        var amountSucceeded = await _promoterRepository.TryActivatePendingCommissionAsync(
            promoterId,
            (decimal)input.CommBaseAmount,
            (decimal)input.CommBonusAmount,
            transaction);
        if (!amountSucceeded)
        {
            _logger.LogWarning(
                "激活团长待结算余额失败: {PromoterId}",
                promoterId);
        }
    }

    private async Task<CustomerAccount[]> FindCustomerAccountsAsync(
        string? customerId = null,
        string? openId = null,
        string? phone = null,
        string? boundPromoterId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = await _customerRepository.FindCustomerAccountsAsync(
            customerId,
            openId,
            phone,
            boundPromoterId);
        return accounts.ToArray();
    }

    private static decimal TryParseDecimal(string? value)
    {
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

}

public sealed class GroupBDailyMaintenanceService
{
    private static readonly TimeSpan RefundWindow = TimeSpan.FromDays(14);

    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPromoterRepository _promoterRepository;
    private readonly IGroupCInterface _groupCInterface;
    private readonly IOrderTransactionManager _transactionManager;
    private readonly ILogger<GroupBDailyMaintenanceService> _logger;

    public GroupBDailyMaintenanceService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IPromoterRepository promoterRepository,
        IGroupCInterface groupCInterface,
        IOrderTransactionManager transactionManager,
        ILogger<GroupBDailyMaintenanceService> logger)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _promoterRepository = promoterRepository;
        _groupCInterface = groupCInterface;
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public async Task RunDailyChecksAsync(CancellationToken cancellationToken = default)
    {
        await RunOrderExpiryCheckAsync(cancellationToken);
        await RunBindingExpiryCheckAsync(cancellationToken);
    }

    public async Task RunOrderExpiryCheckAsync(CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.Now.AddDays(-14);
        var orders = await _orderRepository.GetOrdersForCommissionExpiryAsync(threshold);

        foreach (var order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _transactionManager.ExecuteAsync(async transaction =>
            {
                var lockedOrder = await _orderRepository.GetByIdForUpdateAsync(order.OrderId, transaction);
                if (lockedOrder == null)
                    return;

                var referenceTime = lockedOrder.CommSettlementDate ?? lockedOrder.CreatedAt;
                if (DateTime.Now - referenceTime < RefundWindow)
                    return;

                var promoterId = lockedOrder.PromoterId?.ToString() ?? string.Empty;
                if (lockedOrder.OrderStatus == OrderStatusCodes.Paid)
                {
                    var commission = await _groupCInterface.CommissionSettlementAsync(
                        new CommissionSettlementInput
                        {
                            PromoterID = promoterId,
                            FinalAmount = (double)lockedOrder.FinalAmount,
                            GoodsAmount = (double)lockedOrder.TotalAmount
                        },
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(promoterId) && int.TryParse(promoterId, out var parsedPromoterId))
                    {
                        if (!await _promoterRepository.TryAddPendingCommissionAsync(
                            parsedPromoterId,
                            (decimal)commission.CommBaseAmount,
                            (decimal)commission.CommBonusAmount,
                            lockedOrder.TotalAmount,
                            transaction))
                        {
                            throw new OrderBusinessException("订单待结算佣金写回失败");
                        }
                    }

                    if (!await _orderRepository.TryUpdateCommissionSettlementAsync(
                        lockedOrder.OrderId,
                        (decimal)commission.CommBaseAmount,
                        (decimal)commission.CommBonusAmount,
                        commission.CommSettlementDate,
                        transaction))
                    {
                        throw new OrderBusinessException("订单佣金结算写回失败");
                    }

                    if (!string.IsNullOrWhiteSpace(promoterId))
                    {
                        await _groupCInterface.ActivatePromoterMoneyAsync(
                            new ActivatePromoterMoneyInput
                            {
                                PromoterID = promoterId,
                                CommBaseAmount = commission.CommBaseAmount,
                                CommBonusAmount = commission.CommBonusAmount
                            },
                            transaction,
                            cancellationToken);
                    }
                }
                else if (lockedOrder.OrderStatus == OrderStatusCodes.Shipped)
                {
                    if (!string.IsNullOrWhiteSpace(promoterId))
                    {
                        await _groupCInterface.ActivatePromoterMoneyAsync(
                            new ActivatePromoterMoneyInput
                            {
                                PromoterID = promoterId,
                                CommBaseAmount = (double)(lockedOrder.CommBaseAmount ?? 0m),
                                CommBonusAmount = (double)(lockedOrder.CommBonusAmount ?? 0m)
                            },
                            transaction,
                            cancellationToken);
                    }
                }

                if (!await _orderRepository.TryUpdateStatusAsync(
                    lockedOrder.OrderId,
                    OrderStatusCodes.Parse(lockedOrder.OrderStatus),
                    OrderStatus.Completed,
                    transaction))
                {
                    throw new OrderBusinessException("订单退款期状态更新失败");
                }
            });
        }
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
                if (lockedCustomer == null || lockedCustomer.BindExpireTime == null)
                    return;

                if (lockedCustomer.BindExpireTime > DateTime.Now)
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
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        await RunOnceAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<GroupBDailyMaintenanceService>();
            await maintenance.RunDailyChecksAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "每日团长与订单巡检执行失败");
        }
    }
}
