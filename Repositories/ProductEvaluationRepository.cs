using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public sealed class ProductEvaluationRepository(IConfiguration configuration)
    : B_BaseRepository(configuration), IProductEvaluationRepository
{
    public Task<bool> ExistsForOrderDetailAsync(
        string orderDetailId,
        IDbTransaction? transaction = null) =>
        WithConnectionAsync(transaction, async connection =>
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Biz_ProductEvaluations WHERE OrderDetailId = :OrderDetailId",
                new { OrderDetailId = orderDetailId }, transaction) > 0);

    public Task InsertAsync(ProductEvaluation evaluation, IDbTransaction transaction) =>
        WithConnectionAsync(transaction, connection => connection.ExecuteAsync(
            @"INSERT INTO Biz_ProductEvaluations (
                  EvaluationId, OrderDetailId, OrderId, ProductId, PromoterId, CustomerId,
                  HighQuality, FastShipping, GoodPackaging, CostEffective, Affordable,
                  ReliablePromoter, CreatedAt)
              VALUES (
                  :EvaluationId, :OrderDetailId, :OrderId, :ProductId, :PromoterId, :CustomerId,
                  :HighQuality, :FastShipping, :GoodPackaging, :CostEffective, :Affordable,
                  :ReliablePromoter, SYSDATE)", evaluation, transaction));

    public async Task<HashSet<string>> GetEvaluatedOrderDetailIdsAsync(
        IEnumerable<string> orderDetailIds,
        IDbTransaction? transaction = null)
    {
        var ids = orderDetailIds.Where(GroupBIds.IsValid).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return [];

        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<string>(
                "SELECT OrderDetailId FROM Biz_ProductEvaluations WHERE OrderDetailId IN :OrderDetailIds",
                new { OrderDetailIds = ids }, transaction)).ToHashSet(StringComparer.Ordinal));
    }

    public async Task<IReadOnlyList<ProductEvaluation>> GetByOrderDetailIdsAsync(
        IEnumerable<string> orderDetailIds,
        IDbTransaction? transaction = null)
    {
        var ids = orderDetailIds.Where(GroupBIds.IsValid).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return [];

        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<ProductEvaluation>(
                @"SELECT EvaluationId, OrderDetailId, OrderId, ProductId, PromoterId, CustomerId,
                         HighQuality, FastShipping, GoodPackaging, CostEffective, Affordable,
                         ReliablePromoter, CreatedAt
                  FROM Biz_ProductEvaluations
                  WHERE OrderDetailId IN :OrderDetailIds",
                new { OrderDetailIds = ids }, transaction)).AsList());
    }

    public async Task<ProductEvaluationAggregate> GetSummaryAsync(
        string promoterId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            await connection.QuerySingleAsync<ProductEvaluationAggregate>(
                @"SELECT
                      COUNT(*) AS TotalCount,
                      NVL(SUM(HighQuality), 0) AS HighQualityCount,
                      NVL(SUM(FastShipping), 0) AS FastShippingCount,
                      NVL(SUM(GoodPackaging), 0) AS GoodPackagingCount,
                      NVL(SUM(CostEffective), 0) AS CostEffectiveCount,
                      NVL(SUM(Affordable), 0) AS AffordableCount,
                      NVL(SUM(ReliablePromoter), 0) AS ReliablePromoterCount
                  FROM Biz_ProductEvaluations
                  WHERE PromoterId = :PromoterId",
                new { PromoterId = promoterId }, transaction));
    }
}
