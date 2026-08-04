using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IRefundManager
    {
        public Task<Result> Refund(GroupC_RefundRequest refundRequest); //整体处理退款函数
    }
}
