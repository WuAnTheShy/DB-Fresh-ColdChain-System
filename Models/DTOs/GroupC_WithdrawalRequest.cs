namespace FreshColdChain.Models.DTOs
{
    /// 提现申请请求
    public class GroupC_WithdrawalRequest
    {
        public string PromoterId { get; set; } = string.Empty;
        public string AccountInfo { get; set; } = string.Empty;   // 收款账户信息
        public decimal ApplyAmount { get; set; }
    }
}
