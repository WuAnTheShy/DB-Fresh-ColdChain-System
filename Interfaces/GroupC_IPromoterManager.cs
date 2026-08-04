using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;

namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IProMonterManager
    {
        Task<CommissionResult> CommissionSettlement(string? promoterID, decimal finalAmount, decimal goodsAmount);                       //佣金结算函数
        Task<Result> ActivatePromoterMoney(string? promoterID, decimal commBaseAmount, decimal commBonusAmount);                         //过退款期佣金二段结算函数
        Task<GroupC_PromoterRegisterResult> RegisterPromoter(GroupC_PromoterRegisterInfo registerInfo);                                  //注册团长信息函数
    }
}
