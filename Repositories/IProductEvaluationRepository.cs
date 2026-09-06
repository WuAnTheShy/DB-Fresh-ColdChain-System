using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IProductEvaluationRepository
{
    Task<bool> ExistsForOrderDetailAsync(string orderDetailId, IDbTransaction? transaction = null);

    Task InsertAsync(ProductEvaluation evaluation, IDbTransaction transaction);

    Task<HashSet<string>> GetEvaluatedOrderDetailIdsAsync(
        IEnumerable<string> orderDetailIds,
        IDbTransaction? transaction = null);

    Task<ProductEvaluationAggregate> GetSummaryAsync(
        string promoterId,
        IDbTransaction? transaction = null);
}
