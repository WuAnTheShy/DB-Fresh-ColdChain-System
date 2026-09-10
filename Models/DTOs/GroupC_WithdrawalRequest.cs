using FreshColdChain.Models;

namespace FreshColdChain.Models.DTOs
{
    // 提现申请请求
    // 不携带收款账号：收款账户以 Crm_Promoters 的绑定账户为唯一事实来源，
    // 由服务端按 AccountPlatform 现场取号并快照到提现记录，避免客户端传入的
    // 拼接账户串与服务端状态不一致。
    public class GroupC_WithdrawalRequest
    {
        public string PromoterId { get; set; } = string.Empty;
        public string AccountPlatform { get; set; } = PromoterPayAccounts.WeChat;
        public decimal ApplyAmount { get; set; }
    }
}
