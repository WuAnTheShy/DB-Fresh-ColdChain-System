using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;

namespace FreshColdChain.Services;

/// <summary>
/// C 组正式适配前使用的佣金 Dummy，不访问 C 组数据表。
/// </summary>
public sealed class DummyCommissionService : ICommissionService
{
    public Task RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
