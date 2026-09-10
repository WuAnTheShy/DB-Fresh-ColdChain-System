using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

// B 组通过 A 组现有公开服务读取商品和库存的内部网关。
public interface IGroupAInventoryGateway
{
    Task<IReadOnlyList<InventoryProductSnapshot>> CheckAvailabilityAsync(
        IReadOnlyList<InventoryAvailabilityItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
