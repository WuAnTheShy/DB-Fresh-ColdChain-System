using DBFreshColdChain.Models;

namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_IProMonterManager
    {
        bool CommisionSettlement(string promoterID, decimal finalAmount, decimal goodsAmount, ref CommissionInfo commissionInfo); //佣金结算函数
        bool ActivatePromoterMoney(string? promoterID, decimal commBaseAmount, decimal commBonusAmount);                          //过退款期佣金二段结算函数
        PromoterRegisterResult RegisterPromoter(PromoterRegisterInfo registerInfo);                                                                 //注册团长信息函数
    }
}
