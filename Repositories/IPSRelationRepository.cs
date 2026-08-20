using System.Data;
namespace DBFreshColdChainSystem.Repositories
{
    public interface IPromoterSupplierRepository
    {
        // 添加关系（幂等：存在则更新状态）
        Task<bool> AddOrUpdateRelationAsync(string promoterId, string supplierId, string status = "Active", IDbTransaction? transaction = null);
        // 删除关系（软删除：将状态设为Inactive）
        Task<bool> SoftDeleteRelationAsync(string promoterId, string supplierId, IDbTransaction? transaction = null);
        // 查询团长所有有效合作的供应商ID列表
        Task<List<string>> GetActiveSupplierIdsByPromoterAsync(string promoterId, IDbTransaction? transaction = null);
        // 批量验证关系是否存在（返回每个supplierId是否有效）
        Task<Dictionary<string, bool>> ValidateRelationsAsync(string promoterId, List<string> supplierIds, IDbTransaction? transaction = null);
        // 检查单个关系是否有效
        Task<bool> IsRelationActiveAsync(string promoterId, string supplierId, IDbTransaction? transaction = null);
    }
}
