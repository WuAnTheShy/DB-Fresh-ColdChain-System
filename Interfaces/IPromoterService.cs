using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Models.CrossGroup;
using FreshColdChain.Models.CrossGroup_C;
using System.Data;
namespace FreshColdChain.Interfaces
{
    public interface IPromoterService
    {
        // 查询可用团长列表
        Task<GroupC_PagedResult<GroupC_AvailablePromoterDto>> GetAvailablePromotersAsync(
        GroupC_AvailablePromoterQuery query,
        CancellationToken cancellationToken = default);

        // 查询团长基本信息
        Task<GroupC_PromoterBasicInfoDto?> GetPromoterBasicInfoAsync(
            string promoterId,
            CancellationToken cancellationToken = default);


        // 查询团长所有合作供应商ID
        Task<List<string>> GetActiveSupplierIdsAsync(string promoterId);
        // 批量验证团长与多个供应商的合作关系（返回每个供应商是否有效）
        Task<Dictionary<string, bool>> ValidateSuppliersAsync(string promoterId, List<string> supplierIds);

        // 消费者绑定团长：插入 CRM_PCR 记录（B组绑定按钮调用）
        Task<Result> BindCustomerToPromoterAsync(
            string customerId,
            string promoterId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default);

        // 查询消费者已绑定的团长ID列表（B组搜索页判断是否已绑定）
        Task<List<string>> GetBoundPromoterIdsAsync(string customerId);

        // 消费者取消关注团长；关系不存在时按幂等成功处理。
        Task<Result> UnbindCustomerFromPromoterAsync(
            string customerId,
            string promoterId,
            CancellationToken cancellationToken = default);

        // 查询绑定了指定团长的消费者列表（团长端「我的消费者」实时读 CRM_PCR）
        Task<List<GroupC_CrmPCRelation>> GetBoundCustomersByPromoterAsync(string promoterId);

        // 团长自助修改头像（仅限系统预置头像白名单）
        Task<Result> UpdatePromoterAvatarAsync(string promoterId, string? avatar);

        // 绑定或更新收款账户（微信 / 支付宝 / 银行卡）
        Task<Result> BindPayAccountAsync(string promoterId, string platform, string accountNo);

        // 解绑收款账户
        Task<Result> UnbindPayAccountAsync(string promoterId, string platform);
    }
}

