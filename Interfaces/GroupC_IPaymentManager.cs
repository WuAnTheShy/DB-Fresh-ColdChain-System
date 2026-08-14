using DBFreshColdChain.Models;
namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IPaymentManager
    {
        bool CreatePaymentRecord(string? orderID, string? payMethod, string? transactionNo, decimal payAmount, string? status, string? errorMessage); //创建支付流水函数
    }
}
