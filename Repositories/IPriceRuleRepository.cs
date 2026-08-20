//操作BizPriceRule表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPriceRuleRepository : IBaseRepository<BizPriceRule>
{
    /// <summary>按产品 ID 查询所有价格规则</summary>
    Task<List<BizPriceRule>> GetByProductIdAsync(string productId);

    /// <summary>按产品 ID 查询当前启用的规则（有效期内，按优先级升序）</summary>
    Task<List<BizPriceRule>> GetActiveByProductIdAsync(string productId, DateTime? referenceTime = null);
}
