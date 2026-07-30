namespace DBFreshColdChain.Models
{
    public class CreatePaymentRequest
    {
        public string PayId { get; set; } = string.Empty;           //支付流水号
        public string PayMethod { get; set; } = string.Empty;       //支付方式
        public string? TransactionNo { get; set; }                  //交易流水号
    }
}
