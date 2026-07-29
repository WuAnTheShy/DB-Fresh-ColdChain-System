namespace DBFreshColdChain.Models
{
    public class RefundRequest
    {
        public string PayID { get; set; }                        //支付流水号
        public decimal Amount { get; set; }                      //退款金额
        public string RefundReason { get; set; } = string.Empty; //退款理由，可加可不加
    }
}
