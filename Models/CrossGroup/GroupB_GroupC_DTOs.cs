namespace DBFreshColdChain.Models.CrossGroup
{
    //存放所有BC之间的DTO

    //Result类
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
    //Request类
    public class CommissionOrderRequest
    {
        public string? promoterID { get; set; } = string.Empty;            //团长编号
        public decimal finalAmount { get; set; } = 0;                      //实付金额
        public decimal goodsAmount { get; set; } = 0;                      //商品金额
    }
    public class ActivateCommissionOrderRequest
    {
        public string? promoterID { get; set; } = string.Empty;            //团长编号
        public decimal commBaseAmount { get; set; } = 0;                   //基础佣金
        public decimal commBonusAmount { get; set; } = 0;                  //奖励佣金
    }
    public class PaymentRequest
    {
        public string? orderID { get; set; } = string.Empty;                //订单号
        public string? payMethod{ get; set; } = string.Empty;               //支付方式
        public string? transactionNo { get; set; } = string.Empty;          //交易流水号
        public decimal payAmount { get; set; } = 0;                         //支付金额
        public string? status { get; set; } = string.Empty;                 //支付状态
        public string? errorMessage { get; set; } = string.Empty;           //支付失败信息
    }
}

