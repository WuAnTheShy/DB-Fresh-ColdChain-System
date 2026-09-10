using System.ComponentModel.DataAnnotations.Schema;

namespace FreshColdChain.Models
{
    public class GroupC_FinWithdrawalRecord
    {
        public string WithdrawalId { get; set; } = string.Empty;     // 提现编号 (主键)
        public string PromoterId { get; set; } = string.Empty;       // 团长编号 (外键, 关联 Crm_Promoters)
        public decimal ApplyAmount { get; set; }                     // 申请提现金额

        // 收款账户按 1NF 拆成 2 个原子列（申请时的账户快照）。
        // 原复合列 AccountInfo 已在拆分迁移中删除。
        public string AccountPlatform { get; set; } = PromoterPayAccounts.WeChat;  // 收款平台: WeChat / Alipay / BankCard
        public string? AccountNo { get; set; }                                     // 收款账号（空表示当时未绑定）

        // 收款账户展示串（平台：账号）。数据库侧已无此列，这里用只读计算属性拼出：
        // 无 setter，Dapper 映射与手写 INSERT 都不会写它；展示层无需改动。
        [NotMapped]
        public string AccountInfo => PromoterPayAccounts.FormatAccountInfo(AccountPlatform, AccountNo);

        public DateTime ApplyTime { get; set; }                      // 申请时间
        public string AuditStatus { get; set; } = "Pending";         // 审核状态: Pending / Approved / Rejected / Paid / Cancelled
        public string AuditorUserId { get; set; } = string.Empty;    // 审核人用户编号 (关联 Sys_Users)
        public DateTime? AuditTime { get; set; }                     // 审核时间
        public string RejectReason { get; set; } = string.Empty;     // 驳回原因
        public DateTime? TransferTime { get; set; }                  // 实际打款时间
    }
}

