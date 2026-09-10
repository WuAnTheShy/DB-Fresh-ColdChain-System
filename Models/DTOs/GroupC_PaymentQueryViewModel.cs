namespace FreshColdChain.Models.DTOs
{
    // 管理端"支付流水与退款记录"查询页视图模型
    public class GroupC_PaymentQueryViewModel
    {
        // 查询条件（回显用）
        public DateTime? StartDate { get; set; }                 // 开始日期（含当天）
        public DateTime? EndDate { get; set; }                   // 结束日期（含当天）
        public string? OrderId { get; set; }                     // 订单编号（精确匹配）
        public string? PayStatus { get; set; }                   // 支付状态: Success / Failed
        public string? RefundStatus { get; set; }                // 退款审核状态: Pending / Approved / Rejected

        // 查询结果
        public List<GroupC_FinPaymentRecord> Payments { get; set; } = new();
        public List<FinRefund> Refunds { get; set; } = new();
    }
}
