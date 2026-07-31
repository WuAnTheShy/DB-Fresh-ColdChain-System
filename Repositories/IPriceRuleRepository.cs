using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories;

public interface IPriceRuleRepository : IBaseRepository<BizPriceRule>
{
    Task<List<BizPriceRule>> GetByProductIdAsync(string productId);
}
