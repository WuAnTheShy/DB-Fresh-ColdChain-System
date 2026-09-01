using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services
{
    public sealed class GroupCInterfaceService : IGroupCInterface
    {
    private static readonly TimeSpan RefundWindow = TimeSpan.FromDays(14);

    private readonly ICustomerRepository _customerRepository;
    private readonly IPointRepository _pointRepository;
    private readonly IPromoterRepository _promoterRepository;
    private readonly ILogger<GroupCInterfaceService> _logger;

    public GroupCInterfaceService(
        ICustomerRepository customerRepository,
        IPointRepository pointRepository,
        IPromoterRepository promoterRepository,
        ILogger<GroupCInterfaceService> logger)
    {
        _customerRepository = customerRepository;
        _pointRepository = pointRepository;
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
}

namespace FreshColdChain.Interfaces
{
    public interface IGroupCInterface
    {
    Task<CustomerAccount[]> FindCustomerAccountInfoAsync(
        string customerID,
        CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByOpenIdAsync(
        string openID,
        CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByPhoneAsync(
        string phone,
        CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByBoundPromoterIdAsync(
        string boundPromoterID,
        CancellationToken cancellationToken = default);

    Task<bool> WriteTableChangeLogAsync(
        TableChangeLogInput input,
        CancellationToken cancellationToken = default);

    Task<bool> AuditOrderAsync(
        AuditOrderInput input,
        CancellationToken cancellationToken = default);

    Task<CommissionInfo> CommissionSettlementAsync(
        CommissionSettlementInput input,
        CancellationToken cancellationToken = default);

    Task PaymentRecordReactionAsync(
        PaymentRecordReactionInput input,
        CancellationToken cancellationToken = default);

    Task<bool> RefundRollbackMoneyAsync(
        RefundRollbackMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<PromBind> FindPromoterInfoAsync(
        string inviteCode,
        string promoterName,
        CancellationToken cancellationToken = default);

    Task ActivatePromoterMoneyAsync(
        ActivatePromoterMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    }
}

namespace FreshColdChain.Models
{
    public sealed class TableChangeLogInput
    {
    public string? TableName { get; set; }
    public string? ActionType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? OperatorType { get; set; }
}

public sealed class AuditOrderInput
{
    public List<AuditOrderItem> GoodsList { get; set; } = [];
}

public sealed class AuditOrderItem
{
    public int ProductID { get; set; }
    public int Quantity { get; set; }
}

public sealed class CommissionSettlementInput
{
    public string PromoterID { get; set; } = string.Empty;
    public double FinalAmount { get; set; }
    public double GoodsAmount { get; set; }
}

public sealed class CommissionInfo
{
    public double CommBaseAmount { get; set; }
    public double CommBonusAmount { get; set; }
    public DateTime CommSettlementDate { get; set; } = DateTime.Now;
}

public sealed class PaymentRecordReactionInput
{
    public string OrderID { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PayMethod { get; set; } = string.Empty;
}

public sealed class RefundRollbackMoneyInput
{
    public string Status { get; set; } = string.Empty;
    public string PromoterID { get; set; } = string.Empty;
    public string? CommBaseAmount { get; set; }
    public string? CommBonusAmount { get; set; }
    public double? GoodsAmount { get; set; }
}

public sealed class PromBind
{
    public bool IsValid { get; set; }
    public string? BoundPromoterID { get; set; }
    public DateTime? BindExpireTime { get; set; }
    public string? Avatar { get; set; }
    public string? AvatarUrl { get; set; }
}

    public sealed class ActivatePromoterMoneyInput
    {
        public string PromoterID { get; set; } = string.Empty;
        public double CommBaseAmount { get; set; }
        public double CommBonusAmount { get; set; }
    }
}
