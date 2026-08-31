using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

public static class CheckoutPaymentMethods
{
    public const string WeChat = "WECHAT";
    public const string Alipay = "ALIPAY";
    public const string BankCard = "BANK_CARD";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        WeChat, Alipay, BankCard
    };
}

public sealed class CheckoutBatchPaymentRequest
{
    [Required]
    [StringLength(20)]
    public string PaymentMethod { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BankName { get; set; }
}

public sealed class CheckoutBatchSummary
{
    public string CheckoutBatchId { get; init; } = string.Empty;
    public DateTime PaymentExpiresAt { get; init; }
    public string OrderStatus { get; init; } = string.Empty;
    public decimal FinalAmount { get; init; }
    public int ChildOrderCount { get; init; }
    public IReadOnlyList<CheckoutBatchOrderSummary> Orders { get; init; } = [];
}

public sealed class CheckoutBatchOrderSummary
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string PromoterId { get; init; } = string.Empty;
    public decimal FinalAmount { get; init; }
    public string OrderStatus { get; init; } = string.Empty;
}

public sealed class CheckoutBatchPaymentResult
{
    public string CheckoutBatchId { get; init; } = string.Empty;
    public string TransactionNo { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? BankName { get; init; }
    public decimal PaidAmount { get; init; }
    public int ChildOrderCount { get; init; }
    public bool AlreadyPaid { get; init; }
    public bool IsExpired { get; init; }
}
