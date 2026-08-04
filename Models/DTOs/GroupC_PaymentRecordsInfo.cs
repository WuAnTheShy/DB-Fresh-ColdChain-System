namespace DBFreshColdChain.Models.DTOs
{
    public class GroupC_FinPaymentRecord
    {
        public string PayId { get; set; } = string.Empty;            // 支付流水编号 (主键)
        public string OrderId { get; set; } = string.Empty;          // 订单编号 (外键, 关联 Biz_Orders)
        public string PayMethod { get; set; } = string.Empty;        // 支付方式 (微信/支付宝/银行卡等)
        public string? TransactionNo { get; set; } = string.Empty;    // 第三方交易流水号
        public decimal PayAmount { get; set; }                       // 支付金额
        public string Status { get; set; } = "Pending";              // 支付状态:  Success / Failed 
        public DateTime? PayTime { get; set; }                       // 记录时间
        public string Remark { get; set; } = string.Empty;           // 备注
    }
}

