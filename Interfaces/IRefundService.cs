using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.CrossGroup_C;
namespace FreshColdChain.Interfaces
{
    public interface IRefundService
    {
        //整体处理退款函数（自建事务)
        //B组直接使用，内含退款所有逻辑
        public Task<Result> Refund(GroupC_RefundRequest refundRequest); 
    }
}
