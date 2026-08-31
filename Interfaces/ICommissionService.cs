using FreshColdChain.Models.CrossGroup_C;
using System.Data;

namespace FreshColdChain.Interfaces;

public interface ICommissionService
{
    Task<CommissionResult> RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<Result> ActivatePromoterMoney(
        ActivateCommissionOrderRequest request,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
