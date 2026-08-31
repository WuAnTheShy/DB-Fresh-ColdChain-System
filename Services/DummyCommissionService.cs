using System.Data;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.CrossGroup_C;

namespace FreshColdChain.Services;

/// <summary>
/// C 组正式适配前使用的佣金 Dummy，不访问 C 组数据表。
/// </summary>
public sealed class DummyCommissionService : ICommissionService
{
    public Task<CommissionResult> RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CommissionResult
        {
            IsSuccess = true,
            CommSettlementDate = DateTime.Now
        });
    }

    public Task<Result> ActivatePromoterMoney(
        ActivateCommissionOrderRequest request,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Result { IsSuccess = true });
    }
}
