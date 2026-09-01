namespace FreshColdChain.Models;

/// <summary>B 组下单前通过 A 组公开服务校验的商品与数量。</summary>
public sealed class InventoryAvailabilityItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
