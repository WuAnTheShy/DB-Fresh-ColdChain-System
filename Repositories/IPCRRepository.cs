using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories
{
    public interface IPCRRepository
    {
        Task<bool> InsertRelationAsync(string customerId, string promoterId, IDbTransaction? transaction = null);

        Task<bool> ExistsRelationAsync(string customerId, string promoterId, IDbTransaction? transaction = null);

        Task<List<string>> GetPromoterIdsByCustomerAsync(string customerId, IDbTransaction? transaction = null);

        Task<List<GroupC_CrmPCRelation>> GetRelationsByPromoterAsync(string promoterId, IDbTransaction? transaction = null);
    }
}
