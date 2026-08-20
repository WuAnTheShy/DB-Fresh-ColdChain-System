using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface IPromoterService
    {
        //查询可用团长列表
        Task<GroupC_PagedResult<GroupC_AvailablePromoterDto>> GetAvailablePromotersAsync(
        GroupC_AvailablePromoterQuery query,
        CancellationToken cancellationToken = default);

        //查询团长基本信息
        Task<GroupC_PromoterBasicInfoDto?> GetPromoterBasicInfoAsync(
            string promoterId,
            CancellationToken cancellationToken = default);


        // 查询团长所有合作供应商ID
        Task<List<string>> GetActiveSupplierIdsAsync(string promoterId);
        // 批量验证团长与多个供应商的合作关系（返回每个供应商是否有效）
        Task<Dictionary<string, bool>> ValidateSuppliersAsync(string promoterId, List<string> supplierIds);
    }
}

