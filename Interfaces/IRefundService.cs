using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface IRefundService
    {
        //整体处理退款函数（自建事务)
        //B组直接使用，内含退款所有逻辑
        public Task<Result> Refund(GroupC_RefundRequest refundRequest); 
    }
}
