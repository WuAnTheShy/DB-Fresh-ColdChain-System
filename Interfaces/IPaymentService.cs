using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models.CrossGroup_C;
using System.Data;
namespace FreshColdChain.Interfaces
{
    public interface IPaymentService
    {
        //创建支付流水函数接口
        Task<Result> CreatePaymentRecord(PaymentRequest paymentRequest,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default); 
    }
}
