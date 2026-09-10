using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 货物表（Inv_Goods）仓储，主码为 (ProductID, SupplierID) 复合键。
public interface IGoodsRepository
{
    // 查询某供应商的全部货物（含物品与供应商名称）
    Task<List<InvGoods>> GetBySupplierAsync(string supplierId);

    // 查询全部货物（管理员用，含物品与供应商名称）
    Task<List<InvGoods>> GetAllAsync(string? keyword = null);

    // 查询某物品的全部供应商货物（含供应商名称）
    Task<List<InvGoods>> GetByProductAsync(string productId);

    // 查询单件货物（按物品+供应商）
    Task<InvGoods?> GetAsync(string productId, string supplierId);

    // 新增货物
    Task AddAsync(InvGoods goods);

    // 更新货物（售价/上下架/温区/保质期/描述）
    Task UpdateAsync(InvGoods goods);

    // 删除货物
    Task DeleteAsync(string productId, string supplierId);

    // 按“物品”批量设置状态：该物品下所有供应商的货物统一置为目标状态（商品下架联动用），返回受影响行数
    Task<int> UpdateStatusByProductAsync(string productId, string status, DateTime updateTime);
}
