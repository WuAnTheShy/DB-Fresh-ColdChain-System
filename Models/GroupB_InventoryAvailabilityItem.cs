namespace FreshColdChain.Models;

// B 组下单前通过 A 组公开服务校验的商品与数量。
public sealed class InventoryAvailabilityItem
{
    public string ProductId { get; init; } = string.Empty;

    // 供货供应商ID：库存可用量按 (商品, 供应商) 维度校验。
    public string SupplierId { get; init; } = string.Empty;

    public int Quantity { get; init; }
}
