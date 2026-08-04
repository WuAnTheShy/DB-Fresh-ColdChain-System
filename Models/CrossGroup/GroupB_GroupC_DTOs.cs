namespace DBFreshColdChain.Models.CrossGroup
{
    //存放所有BC之间的DTO
    //通用Result类
    public class Result
    {
        public bool IsSuccess { get; set; } = false;                //是否成功
        public string ErrorMessage { get; set; } = string.Empty;    //错误信息
    }
    public class CommissionResult:Result
    {
        public decimal CommBaseAmount { get; set; } = 0;            //基础佣金
        public decimal CommBonusAmount { get; set; } = 0;           //奖励佣金
        public DateTime? CommSettlementDate { get; set; }           //结算时间
    }


}

