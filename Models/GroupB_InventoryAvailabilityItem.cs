namespace FreshColdChain.Models;

/// <summary>B 组下单前通过 A 组公开服务校验的商品与数量。</summary>
public sealed class InventoryAvailabilityItem
{
    public string ProductId { get; init; } = string.Empty;
    /// <summary>由 B 组根据团长在团商品服务端解析，不信任客户端传入。</summary>
    public string? SupplierId { get; init; }
    public int Quantity { get; init; }
}
