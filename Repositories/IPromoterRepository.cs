using System.Data;
using DBFreshColdChain.Models.DTOs;

namespace DBFreshColdChain.Repositories
{
    public interface IPromoterRepository
    {
        //需要事务：业务逻辑
        Task<GroupC_CrmPromoter?> GroupC_FindPromoterRecordAsync(string? promoterId, IDbTransaction? transaction = null);           //查找团长信息
        Task GroupC_UpdatePromoterTotalSalesAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null);     //修改团长累计销售额
        Task GroupC_UpdatePromoterPendingBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null); //修改团长待结算余额
        Task<decimal?> GroupC_FindPromoterPendingBalanceAsync(string? promoterId, IDbTransaction? transaction = null);              //查找团长待结算余额
        Task GroupC_UpdatePromoterCurrentBalanceAsync(string? promoterId, decimal deltaAmount, IDbTransaction? transaction = null); //查找团长可提现余额
        Task<bool> GroupC_ExistsPromoterByLoginAccountAsync(string loginAccount, IDbTransaction? transaction = null);               //检查团长账号是否存在
        Task<bool> GroupC_InsertPromoterAsync(GroupC_CrmPromoter promoter, IDbTransaction? transaction = null);                     //新插入团长账号信息
        //无需事务：登录账号时查找团长账号信息
        GroupC_CrmPromoter? GroupC_FindPromoterByLoginAccount(string loginAccount);             //查找团长账号信息

    }
}
