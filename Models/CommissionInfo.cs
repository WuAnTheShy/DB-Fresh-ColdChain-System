namespace DBFreshColdChain.Models
{
    public class CommissionInfo
    {
        public decimal CommBaseAmount { get; set; } = 0;     //基础佣金
        public decimal CommBonusAmount { get; set; } = 0;    //奖励佣金
        public DateTime? CommSettlementDate { get; set; }   //结算时间
    }

}
