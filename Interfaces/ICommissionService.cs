using DBFreshColdChain.Models.CrossGroup;
using System.Data;

namespace DBFreshColdChain.Interfaces
{
    public interface ICommissionService
    {
        //佣金一段结算（签收时）
        Task<CommissionResult> RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
        //佣金二段结算(过退款期)
        Task<Result> ActivatePromoterMoney(
            ActivateCommissionOrderRequest request, 
            IDbTransaction? transaction = null, 
            CancellationToken cancellationToken = default);                        
    }
}
  