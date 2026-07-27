using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

/// <summary>
/// B 组调用 C 组佣金模块的跨组契约。
/// C 组实现必须使用传入事务，不能自行提交或回滚。
/// </summary>
public interface ICommissionService
{
    Task RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
