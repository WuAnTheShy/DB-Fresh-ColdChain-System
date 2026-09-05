using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 订单列表查询条件。
/// </summary>
public sealed class OrderQueryRequest
{
    [StringLength(36, ErrorMessage = "消费者ID不能超过36个字符")]
    public string? CustomerId { get; set; }

    public OrderStatus? Status { get; set; }

    [StringLength(50, ErrorMessage = "关键词不能超过50个字符")]
    public string? Keyword { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int Page { get; set; } = 1;

    [Range(1, 50, ErrorMessage = "每页数量必须在1到50之间")]
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// 订单列表行。
/// </summary>
public sealed class OrderListItem
{
    private string? _displayStatusCode;

    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? CheckoutBatchId { get; init; }
    public string? PromoterId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal FinalAmount { get; init; }
    public string OrderStatus { get; init; } = OrderStatusCodes.PendingPayment;
    public int ItemCount { get; init; }
    public int SupplierCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PaymentExpiresAt { get; init; }

    public string DisplayStatusCode
    {
        get => _displayStatusCode ?? OrderStatus;
        set => _displayStatusCode = value;
    }

    public string? DisplayStatusName { get; set; }

    public OrderStatus Status => OrderStatusCodes.Parse(OrderStatus);
    public string StatusName => DisplayStatusName ?? OrderStatusNames.GetName(Status);
}

/// <summary>
/// 带分页信息的订单列表页面模型。
/// </summary>
public sealed class OrderListViewModel
{
    public OrderQueryRequest Query { get; init; } = new();
    public IReadOnlyList<OrderListItem> Orders { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
