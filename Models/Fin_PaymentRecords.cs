namespace DBFreshColdChain.Models
{
    public class FinPaymentRecord
    {
        public string PayId { get; set; } = string.Empty;            // 支付流水编号 (主键)
        public string OrderId { get; set; } = string.Empty;          // 订单编号 (外键, 关联 Biz_Orders)
        public string PayMethod { get; set; } = string.Empty;        // 支付方式 (微信/支付宝/银行卡等)
        public string TransactionNo { get; set; } = string.Empty;    // 第三方交易流水号
        public decimal PayAmount { get; set; }                       // 支付金额
        public string Status { get; set; } = "Pending";              // 支付状态: Pending / Success / Failed / Closed / RefundedPart / RefundedAll
        public DateTime? PayTime { get; set; }                       // 支付成功时间
        public string FailReason { get; set; } = string.Empty;       // 支付失败原因
        public decimal RefundAmount { get; set; }                    // 累计退款金额
        public string Remark { get; set; } = string.Empty;           // 备注
    }
}

