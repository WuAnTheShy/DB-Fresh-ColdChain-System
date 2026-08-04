using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IPaymentManager
    {
        Task<Result> CreatePaymentRecord(string? orderID, string? payMethod, string? transactionNo, decimal payAmount, string? status, string? errorMessage); //创建支付流水函数
    }
}
