using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FreshColdChain.Models;

// 订单列表查询条件。
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

    // 仅服务端内部使用的附加过滤：与 <see cref="Status"/> 取并集的订单号集合。
    // 消费者端「退款售后」列表用（改造前提交、订单状态未进入审核中的历史退款申请单也要列出），
    // 不接受外部查询串传入。
    [BindNever]
    [JsonIgnore]
    public IReadOnlyCollection<string>? OrderIds { get; set; }

    // 仅服务端内部使用的状态集合过滤（优先于 <see cref="Status"/>）。
    // 消费者端「退款售后」列表需要同时覆盖“退款审核中”和“退款中”两种状态，
    // 不接受外部查询串传入。
    [BindNever]
    [JsonIgnore]
    public IReadOnlyCollection<OrderStatus>? OrderStatuses { get; set; }
}

// 订单列表行。
public sealed class OrderListItem
{
    private string? _displayStatusCode;

    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? CheckoutBatchId { get; init; }
    public string? PromoterId { get; init; }
    public string? PromoterName { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal FinalAmount { get; init; }
    public string OrderStatus { get; init; } = OrderStatusCodes.PendingPayment;
    public int ItemCount { get; init; }
    public int SupplierCount { get; init; }
    public string? FirstProductId { get; init; }
    public string? FirstProductName { get; init; }
    public string? FirstProductImageUrl { get; set; }
    public int FirstProductQuantity { get; init; }
    public IReadOnlyList<OrderCardProductItem> ProductItems { get; set; } = [];
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

// 订单列表卡片中的商品行。
public sealed class OrderCardProductItem
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderDetailId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? SupplierId { get; init; }
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubTotal { get; init; }
}

// 带分页信息的订单列表页面模型。
public sealed class OrderListViewModel
{
    public OrderQueryRequest Query { get; init; } = new();
    public IReadOnlyList<OrderListItem> Orders { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
