//操作BizPriceRule表

using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IPriceRuleRepository : IBaseRepository<BizPriceRule>
{
    Task<List<BizPriceRule>> GetByProductIdAsync(string productId);//按产品 ID 查询价格规则列表
}
