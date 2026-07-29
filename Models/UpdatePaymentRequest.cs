namespace DBFreshColdChain.Models
{
    public class UpdatePaymentRequest
    {
        public string PayID { get; set; }                           //支付流水号
        public bool IsSuccess { get; set; }                         //更新是否成功标志位
        public string ErrorMessage { get; set; } = string.Empty;    //错误消息，仅在错误时设置，否则为空
    }

}
