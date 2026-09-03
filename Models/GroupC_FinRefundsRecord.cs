namespace FreshColdChain.Models
{
    public class FinRefund
    {
        public string? RefundId { get; set; } = string.Empty;         // 退款编号 (主键)
        public string? OrderId { get; set; } = string.Empty;          // 订单编号 (外键, 关联 Biz_Orders)
        public string? DetailId { get; set; } = string.Empty;         // 订单明细编号 (外键, 关联 Biz_OrderDetails)
        public string? SupplierId { get; set; } = string.Empty;       // 供应商编号
        public int RefundQty { get; set; }                           // 退款数量
        public decimal RefundAmount { get; set; }                    // 退款金额
        public string? LiabilityType { get; set; } = "Customer";      // 责任归属: Supplier / Platform / Customer / Logistics
        public DateTime ApplyTime { get; set; }                      // 申请时间
        public string? Remark { get; set; } = string.Empty;           // 备注
        public string Status { get; set; } = "Pending";              // 审核状态: Pending / Approved / Rejected / Cancelled
        public DateTime? AuditTime { get; set; }                     // 审核时间
        public string? AuditorId { get; set; }                       // 审核人
    }
}

