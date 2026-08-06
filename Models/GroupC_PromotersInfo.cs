namespace DBFreshColdChain.Models
{
    public class GroupC_CrmPromoter
    {
        public string PromoterId { get; set; } = string.Empty;       // 团长编号 (主键)
        public string PromoterName { get; set; } = string.Empty;     // 团长姓名
        public string Phone { get; set; } = string.Empty;            // 联系电话
        public string InviteCode { get; set; } = string.Empty;       // 邀请码
        public decimal BaseCommissionRate { get; set; }              // 基础佣金比例(如 5.00 表示 5%)
        public decimal CurrentBalance { get; set; }                  // 当前可提现余额
        public decimal PendingBalance { get; set; }                  // 待结算余额
        public decimal FrozenAmount { get; set; }                    // 冻结金额
        public decimal TotalSales { get; set; }                      // 累计有效销售额
        public int TotalOrderCount { get; set; }                     // 累计有效订单数
        public string Status { get; set; } = "Enabled";              // 状态: Enable / Disable / Pending / Frozen
        public DateTime RegisterTime { get; set; }                   // 注册时间
        public DateTime? LastSettlementTime { get; set; }            // 最近一次结算时间
        public string Remark { get; set; } = string.Empty;           // 备注  
        public string LoginAccount { get; set; } = string.Empty;     // 登录账号
        public string LoginPassword { get; set; } = string.Empty;    // 登录密码 
    }
}
