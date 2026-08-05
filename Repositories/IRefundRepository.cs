using System.Data;
using DBFreshColdChain.Models.DTOs;

namespace DBFreshColdChain.Repositories
{
    public interface IRefundRepository
    {
        //需要事务：业务逻辑
        Task InsertRefundAsync(FinRefund refund, IDbTransaction? transaction = null);
    }
}
