//操作InvSupplierPrices表

using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ISupplierPriceRepository : IBaseRepository<InvSupplierPrice>
{
    // 查询某供应商对某产品的当前供货价（无报价返回 null）
    Task<InvSupplierPrice?> GetQuoteAsync(string supplierId, string productId);

    // 查询某供应商的所有供货价
    Task<List<InvSupplierPrice>> GetQuotesBySupplierAsync(string supplierId);

    // 查询某产品所有已报价的供应商（含供应商名称，入库下拉用）
    Task<List<InvSupplierPrice>> GetQuotesByProductWithSupplierAsync(string productId);
}
