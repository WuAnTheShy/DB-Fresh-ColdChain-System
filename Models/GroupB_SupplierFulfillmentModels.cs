using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models;

public sealed class SupplierFulfillmentQuery
{
    public OrderStatus? Status { get; set; }

    [StringLength(50)]
    public string? Keyword { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

public sealed class SupplierFulfillmentOrderListItem
{
    public string OrderId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;

    // 收货地址的 4 个原子列（投影直接取 Biz_Orders 的对应列）
    public string? ReceiverProvince { get; init; }
    public string? ReceiverCity { get; init; }
    public string? ReceiverDistrict { get; init; }
    public string? ReceiverDetailAddress { get; init; }

    // 收货地址展示串（省 市 区 详址）。
    [NotMapped]
    public string ShippingAddress => ReceiverAddress.Format(
        ReceiverProvince,
        ReceiverCity,
        ReceiverDistrict,
        ReceiverDetailAddress);

    public string OrderStatus { get; init; } = OrderStatusCodes.Paid;
    public int ItemCount { get; init; }
    public int TotalQuantity { get; init; }
    public decimal SupplierAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public string StatusName => OrderStatusNames.GetName(OrderStatusCodes.Parse(OrderStatus));
}

public sealed class SupplierFulfillmentListViewModel
{
    public string SupplierId { get; init; } = string.Empty;
    public SupplierFulfillmentQuery Query { get; init; } = new();
    public IReadOnlyList<SupplierFulfillmentOrderListItem> Orders { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class SupplierFulfillmentDetailViewModel
{
    public string SupplierId { get; init; } = string.Empty;
    public BizOrder Order { get; init; } = new();
    public string CustomerName { get; init; } = string.Empty;
    public IReadOnlyList<BizOrderDetail> Items { get; init; } = [];
    public SupplierLogisticsSnapshot Logistics { get; init; } = new();
    public bool CanShip { get; init; }
}
