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
}
