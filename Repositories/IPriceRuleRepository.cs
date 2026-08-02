//操作BizPriceRule表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IPriceRuleRepository : IBaseRepository<BizPriceRule>
{
    Task<List<BizPriceRule>> GetByProductIdAsync(string productId);//按产品 ID 查询价格规则列表
}
