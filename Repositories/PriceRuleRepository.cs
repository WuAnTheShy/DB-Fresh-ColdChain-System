using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class PriceRuleRepository : BaseRepository<BizPriceRule>, IPriceRuleRepository
{
    public PriceRuleRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<BizPriceRule>> GetByProductAsync(string productId, string? supplierId = null)
    {
        if (supplierId == null)
        {
            var sql = """SELECT * FROM Biz_PriceRules WHERE ProductID = :Id ORDER BY RuleID """;
            return (await _uow.Connection.QueryAsync<BizPriceRule>(sql, new { Id = productId }, _uow.Transaction)).ToList();
        }
        var sql2 = """SELECT * FROM Biz_PriceRules WHERE ProductID = :Id AND SupplierID = :SupplierId ORDER BY RuleID """;
        return (await _uow.Connection.QueryAsync<BizPriceRule>(sql2,
            new { Id = productId, SupplierId = supplierId }, _uow.Transaction)).ToList();
    }

    /// <summary>
    /// 查询某商品+供应商当前启用的规则：
    ///   IsActive = 1
    ///   AND (EffectiveFrom IS NULL OR EffectiveFrom <= 参考时间)
    ///   AND (EffectiveTo IS NULL OR EffectiveTo >= 参考时间)
    /// 按 Priority 升序（数字越小越优先）
    /// </summary>
    public async Task<List<BizPriceRule>> GetActiveByProductAsync(string productId, string supplierId, DateTime? referenceTime = null)
    {
        var now = referenceTime ?? DateTime.Now;
        var sql = """
            SELECT * FROM Biz_PriceRules
            WHERE ProductID = :Id AND SupplierID = :SupplierId
              AND (IsActive IS NULL OR IsActive = 1)
              AND (EffectiveFrom IS NULL OR EffectiveFrom <= :Now)
              AND (EffectiveTo IS NULL OR EffectiveTo >= :Now)
            ORDER BY Priority ASC NULLS LAST, RuleID ASC
            """;
        return (await _uow.Connection.QueryAsync<BizPriceRule>(sql,
            new { Id = productId, SupplierId = supplierId, Now = now }, _uow.Transaction)).ToList();
    }
}
