namespace DBFreshColdChain.Models
{
    public class CreatePaymentRequest
    {
        public string OrderId { get; set; }     //订单号
        public string PayMethod { get; set; }   //支付方式
        public string TransactionNo { get; set; }   //交易流水号
    }
}
