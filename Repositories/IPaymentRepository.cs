using DBFreshColdChain.Models.DTOs;
using System.Data;

namespace DBFreshColdChain.Repositories
{
    public interface IPaymentRepository
    {
        Task GroupC_AddPaymentRecordAsync(GroupC_FinPaymentRecord finPaymentRecord, IDbTransaction? transaction = null);
    }

}
