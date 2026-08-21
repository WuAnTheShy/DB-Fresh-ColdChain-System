using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public interface IPaymentRepository
    {
        Task GroupC_AddPaymentRecordAsync(GroupC_FinPaymentRecord finPaymentRecord, IDbTransaction? transaction = null);
    }

}
