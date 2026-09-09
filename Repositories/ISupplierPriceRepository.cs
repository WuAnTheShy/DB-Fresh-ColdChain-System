// 操作InvSupplierPrices表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ISupplierPriceRepository : IBaseRepository<InvSupplierPrice>
{
    /// <summary>查询某供应商对某产品的当前供货价（无报价返回 null）</summary>
    Task<InvSupplierPrice?> GetQuoteAsync(string supplierId, string productId);

    /// <summary>查询某供应商的所有供货价</summary>
    Task<List<InvSupplierPrice>> GetQuotesBySupplierAsync(string supplierId);

    /// <summary>查询某产品所有已报价的供应商（含供应商名称，入库下拉用）</summary>
    Task<List<InvSupplierPrice>> GetQuotesByProductWithSupplierAsync(string productId);
}
