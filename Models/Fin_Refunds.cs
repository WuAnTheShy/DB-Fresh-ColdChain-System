namespace DBFreshColdChain.Models
{
    public class FinRefund
    {
        public string RefundId { get; set; } = string.Empty;         // 退款编号 (主键)
        public string OrderId { get; set; } = string.Empty;          // 订单编号 (外键, 关联 Biz_Orders)
        public string DetailId { get; set; } = string.Empty;         // 订单明细编号 (外键, 关联 Biz_OrderDetails)
        public string SupplierId { get; set; } = string.Empty;       // 供应商编号
        public string RefundType { get; set; } = "OrderRefund";      // 退款类型: OrderRefund / DetailRefund
        public int RefundQty { get; set; }                           // 退款数量
        public decimal RefundAmount { get; set; }                    // 退款金额
        public string RefundReason { get; set; } = string.Empty;     // 退款原因
        public string LiabilityType { get; set; } = "Customer";      // 责任归属: Supplier / Platform / Customer / Logistics
        public string AuditStatus { get; set; } = "Pending";         // 审核状态: Pending / Approved / Rejected / Completed / Closed
        public DateTime ApplyTime { get; set; }                      // 申请时间
        public DateTime? AuditTime { get; set; }                     // 审核时间
        public DateTime? FinishTime { get; set; }                    // 完成时间
        public string AuditorUserId { get; set; } = string.Empty;    // 审核人编号 (关联 Sys_Users)
        public decimal CommissionReversedAmount { get; set; }        // 已冲减佣金金额
        public string PointRollbackStatus { get; set; } = "Pending"; // 积分回滚状态: Pending / Done / Failed
        public string Remark { get; set; } = string.Empty;           // 备注
    }
}

