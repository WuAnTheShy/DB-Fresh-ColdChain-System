using FreshColdChain.Models.CrossGroup_C;
using System.Data;

namespace FreshColdChain.Interfaces
{
    public interface ICommissionService
    {
        //佣金一段结算接口（签收时）
        Task<CommissionResult> RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
        //佣金二段结算接口 (过退款期)
        Task<Result> ActivatePromoterMoney(
            ActivateCommissionOrderRequest request, 
            IDbTransaction? transaction = null, 
            CancellationToken cancellationToken = default);                        
    }
}
  