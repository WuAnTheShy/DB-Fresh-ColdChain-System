using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public class PriceRuleRepository : BaseRepository<BizPriceRule>, IPriceRuleRepository
{
    public PriceRuleRepository(IUnitOfWork uow) : base(uow) { }

    public async Task<List<BizPriceRule>> GetByProductIdAsync(string productId)
    {
        var sql = """SELECT * FROM "Biz_PriceRules" WHERE "ProductID" = :Id ORDER BY "RuleID" """;
        return (await _uow.Connection.QueryAsync<BizPriceRule>(sql, new { Id = productId }, _uow.Transaction)).ToList();
    }

    /// <summary>
    /// 查询某商品当前启用的规则：
    ///   IsActive = 1
    ///   AND (EffectiveFrom IS NULL OR EffectiveFrom <= 参考时间)
    ///   AND (EffectiveTo IS NULL OR EffectiveTo >= 参考时间)
    /// 按 Priority 升序（数字越小越优先）
    /// </summary>
    public async Task<List<BizPriceRule>> GetActiveByProductIdAsync(string productId, DateTime? referenceTime = null)
    {
        var now = referenceTime ?? DateTime.Now;
        var sql = """
            SELECT * FROM "Biz_PriceRules"
            WHERE "ProductID" = :Id
              AND "IsActive" = 1
              AND ("EffectiveFrom" IS NULL OR "EffectiveFrom" <= :Now)
              AND ("EffectiveTo" IS NULL OR "EffectiveTo" >= :Now)
            ORDER BY "Priority" ASC, "RuleID" ASC
            """;
        return (await _uow.Connection.QueryAsync<BizPriceRule>(sql,
            new { Id = productId, Now = now }, _uow.Transaction)).ToList();
    }
}
