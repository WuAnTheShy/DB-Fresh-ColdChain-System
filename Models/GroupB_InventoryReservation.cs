namespace FreshColdChain.Models;

/// <summary>
/// 向 A 组库存模块提交的预留项。
/// </summary>
public sealed class InventoryReservationItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

/// <summary>
/// A 组库存模块返回的可信商品快照。
/// </summary>
public sealed class InventoryProductSnapshot
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string SupplierId { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
}
