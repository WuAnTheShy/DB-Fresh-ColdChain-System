using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null);
    Task<BizOrder?> GetByIdAsync(int orderId, IDbTransaction? transaction = null);
    Task UpdateStatusAsync(int orderId, int status, IDbTransaction? transaction = null);
    Task InsertDetailsAsync(
        IEnumerable<BizOrderDetail> details,
        IDbTransaction? transaction = null);
}
