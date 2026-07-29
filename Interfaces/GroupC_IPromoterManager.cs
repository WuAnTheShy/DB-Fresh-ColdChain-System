using DBFreshColdChain.Models;

namespace DBFreshColdChain.Services
{
    public interface GroupC_IProMonterManager
    {
        bool CommisionSettlement(string promoterID, decimal finalAmount, decimal goodsAmount, ref CommissionInfo commissionInfo); //佣金结算函数
    }
}
