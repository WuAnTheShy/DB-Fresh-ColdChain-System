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
}

public sealed class ActivatePromoterMoneyInput
{
    public string PromoterID { get; init; } = string.Empty;
    public double CommBaseAmount { get; init; }
    public double CommBonusAmount { get; init; }
}
