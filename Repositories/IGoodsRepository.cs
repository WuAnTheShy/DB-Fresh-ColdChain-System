using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>货物表（Inv_Goods）仓储，主码为 (ProductID, SupplierID) 复合键。</summary>
public interface IGoodsRepository
{
    /// <summary>查询某供应商的全部货物（含物品与供应商名称）</summary>
    Task<List<InvGoods>> GetBySupplierAsync(string supplierId);

    /// <summary>查询全部货物（管理员用，含物品与供应商名称）</summary>
    Task<List<InvGoods>> GetAllAsync(string? keyword = null);

    /// <summary>查询某物品的全部供应商货物（含供应商名称）</summary>
    Task<List<InvGoods>> GetByProductAsync(string productId);

    /// <summary>查询单件货物（按物品+供应商）</summary>
    Task<InvGoods?> GetAsync(string productId, string supplierId);

    /// <summary>新增货物</summary>
    Task AddAsync(InvGoods goods);

    /// <summary>更新货物（售价/上下架/温区/保质期/描述）</summary>
    Task UpdateAsync(InvGoods goods);

    /// <summary>删除货物</summary>
    Task DeleteAsync(string productId, string supplierId);
}
