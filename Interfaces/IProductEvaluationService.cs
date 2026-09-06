using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

public interface IProductEvaluationService
{
    Task SubmitAsync(
        string orderId,
        string orderDetailId,
        string customerId,
        ProductEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductEvaluationSummary> GetSummaryAsync(string promoterId);

    Task<HashSet<string>> GetEvaluatedOrderDetailIdsAsync(IEnumerable<string> orderDetailIds);
}
