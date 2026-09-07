// 操作BizPriceRule表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPriceRuleRepository : IBaseRepository<BizPriceRule>
{
    /// <summary>按产品（+可选供应商）查询价格规则；供应商为 null 时返回该产品全部规则</summary>
    Task<List<BizPriceRule>> GetByProductAsync(string productId, string? supplierId = null);

    /// <summary>按产品+供应商查询当前启用的规则（有效期内，按优先级升序）</summary>
    Task<List<BizPriceRule>> GetActiveByProductAsync(string productId, string supplierId, DateTime? referenceTime = null);
}
