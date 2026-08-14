using DBFreshColdChain.Models;
namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IRefundManager
    {
        public bool Refund(RefundRequest refundRequest); //整体处理退款函数
    }
}
