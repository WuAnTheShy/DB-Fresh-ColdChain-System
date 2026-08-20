using DBFreshColdChain.Models.CrossGroup;
using DBFreshColdChain.Models.DTOs;
using System.Data;
namespace DBFreshColdChain.Interfaces
{
    public interface IPaymentService
    {
        //创建支付流水函数接口
        Task<Result> CreatePaymentRecord(PaymentRequest paymentRequest,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default); 
    }
}
