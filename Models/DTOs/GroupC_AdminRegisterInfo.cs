namespace FreshColdChain.Models.DTOs
{
    public class GroupC_AdminRegisterInfo
    {
        public string RealName { get; set; } = string.Empty;
        public string LoginAccount { get; set; } = string.Empty;
        public string LoginPassword { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        /// <summary>管理员种类：ACCOUNT/FINANCE/LOG/PRODUCT</summary>
        public string AdminKind { get; set; } = "ACCOUNT";
    }
}
