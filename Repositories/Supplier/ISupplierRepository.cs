using FreshGroupSystem.Models;

namespace FreshGroupSystem.Repositories.Supplier;

/// <summary>
/// 供应商专用仓储接口（继承通用接口，可扩展供应商特有方法）
/// </summary>
public interface ISupplierRepository : IBaseRepository<Models.Supplier>
{
    /// <summary>
    /// 根据供应商 ID 获取其所有产品
    /// </summary>
    Task<List<Product>> GetProductsBySupplierIdAsync(int supplierId);
}
