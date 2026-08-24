namespace FreshColdChain.Models.DTOs
{
    /// 提现审核通过请求
    public class GroupC_WithdrawApproved
    {
        public string WithdrawalId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;        // 审核人ID
        public DateTime AuditTime { get; set; }                   // 审核时间
    }

}
