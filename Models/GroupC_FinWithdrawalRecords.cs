namespace DBFreshColdChain.Models
{
    public class GroupC_FinWithdrawalRecord
    {
        public string WithdrawalId { get; set; } = string.Empty;     // 提现编号 (主键)
        public string PromoterId { get; set; } = string.Empty;       // 团长编号 (外键, 关联 Crm_Promoters)
        public decimal ApplyAmount { get; set; }                     // 申请提现金额
        public string AccountInfo { get; set; } = string.Empty;      // 收款账户信息
        public DateTime ApplyTime { get; set; }                      // 申请时间
        public string AuditStatus { get; set; } = "Pending";         // 审核状态: Pending / Approved / Rejected / Paid / Cancelled
        public string AuditorUserId { get; set; } = string.Empty;    // 审核人用户编号 (关联 Sys_Users)
        public DateTime? AuditTime { get; set; }                     // 审核时间
        public string RejectReason { get; set; } = string.Empty;     // 驳回原因
        public DateTime? TransferTime { get; set; }                  // 实际打款时间
        public string Remark { get; set; } = string.Empty;           // 备注
    }
}

