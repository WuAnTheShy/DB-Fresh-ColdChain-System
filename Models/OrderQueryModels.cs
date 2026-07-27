using System.ComponentModel.DataAnnotations;

namespace FreshColdChain.Models;

/// <summary>
/// 订单列表查询条件。
/// </summary>
public sealed class OrderQueryRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "消费者ID必须大于0")]
    public int? CustomerId { get; set; }

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
    public int OrderId { get; init; }
    public string OrderNo { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal FinalAmount { get; init; }
    public int OrderStatus { get; init; }
    public int ItemCount { get; init; }
    public int SupplierCount { get; init; }
    public DateTime CreatedAt { get; init; }

    public OrderStatus Status => (OrderStatus)OrderStatus;
    public string StatusName => OrderStatusNames.GetName(Status);
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
