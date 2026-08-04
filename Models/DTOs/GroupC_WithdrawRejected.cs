namespace DBFreshColdChain.Models.DTOs
{
    /// 提现审核驳回请求
    public class GroupC_WithdrawRejected
    {
        public string WithdrawalId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string RejectReason { get; set; } = string.Empty;
        public DateTime AuditTime { get; set; }
    }
}
