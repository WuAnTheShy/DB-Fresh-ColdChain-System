namespace FreshColdChain.Models.DTOs
{
    public class GroupC_UpdatePaymentRequest
    {
        public string PayId { get; set; } = string.Empty;                           //支付流水号
        public bool IsSuccess { get; set; } = false;                        //更新是否成功标志位
        public string ErrorMessage { get; set; } = string.Empty;    //错误消息，仅在错误时设置，否则为空
    }

}
