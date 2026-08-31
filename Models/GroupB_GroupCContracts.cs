using System.ComponentModel.DataAnnotations;
using System.Data;

namespace FreshColdChain.Models;

public sealed class CustomerAccount
{
    public string CustomerID { get; init; } = string.Empty;
    public string? OpenID { get; init; }
    public string Phone { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int GrowthValue { get; init; }
    public string? BoundPromoterID { get; init; }
    public DateTime? BindExpireTime { get; init; }
}

public sealed class TableChangeLogInput
{
    public string? TableName { get; init; }
    public string? ActionType { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? OperatorType { get; init; }
}

public sealed class Goods
{
    [Range(1, int.MaxValue)]
    public int ProductID { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class AuditOrderInput
{
    public IReadOnlyList<Goods> GoodsList { get; init; } = [];
}

public sealed class CommissionSettlementInput
{
    public string PromoterID { get; init; } = string.Empty;
    public double FinalAmount { get; init; }
    public double GoodsAmount { get; init; }
}

public sealed class CommissionInfo
{
    public double CommBaseAmount { get; init; }
    public double CommBonusAmount { get; init; }
    public DateTime CommSettlementDate { get; init; }
}

public sealed class PaymentRecordReactionInput
{
    public string OrderID { get; init; } = string.Empty;
    public string? PayMethod { get; init; }
    public string? TransactionNo { get; init; }
    public string ProductID { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class RefundRollbackMoneyInput
{
    public string OrderNo { get; init; } = string.Empty;
    public string PromoterID { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CommBaseAmount { get; init; }
    public string? CommBonusAmount { get; init; }
    public double? GoodsAmount { get; init; }
}

public sealed class PromBind
{
    public bool IsValid { get; init; }
    public string? BoundPromoterID { get; init; }

    public string? BoundPromoterI
    {
        get => BoundPromoterID;
        init => BoundPromoterID = value;
    }

    public DateTime? BindExpireTime { get; init; }

    // 团长头像标识（cat/rabbit/panda/fox/carrot/broccoli/tomato/corn），未设置时为空
    public string? Avatar { get; init; }

    // 团长头像图片地址（可直接用于 <img src>），未设置时为空
    public string? AvatarUrl { get; init; }
}

public sealed class ActivatePromoterMoneyInput
{
    public string PromoterID { get; init; } = string.Empty;
    public double CommBaseAmount { get; init; }
    public double CommBonusAmount { get; init; }
}

public interface IGroupCInterface
{
    Task<CustomerAccount[]> FindCustomerAccountInfoAsync(string customerID, CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByOpenIdAsync(string openID, CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<CustomerAccount[]> FindCustomerAccountInfoByBoundPromoterIdAsync(string boundPromoterID, CancellationToken cancellationToken = default);

    Task<bool> WriteTableChangeLogAsync(TableChangeLogInput input, CancellationToken cancellationToken = default);

    Task<bool> AuditOrderAsync(AuditOrderInput input, CancellationToken cancellationToken = default);

    Task<CommissionInfo> CommissionSettlementAsync(CommissionSettlementInput input, CancellationToken cancellationToken = default);

    Task PaymentRecordReactionAsync(PaymentRecordReactionInput input, CancellationToken cancellationToken = default);

    Task<bool> RefundRollbackMoneyAsync(
        RefundRollbackMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<PromBind> FindPromoterInfoAsync(string inviteCode, string promoterName, CancellationToken cancellationToken = default);

    Task ActivatePromoterMoneyAsync(
        ActivatePromoterMoneyInput input,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}