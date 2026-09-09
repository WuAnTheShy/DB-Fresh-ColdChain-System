using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;

namespace FreshColdChain.Models.ViewModels
{
    public class PromoterDashboardViewModel
    {
        public GroupC_CrmPromoter Promoter { get; set; } = new();
        public List<PromoterCommissionItemViewModel> RecentCommissions { get; set; } = new();
        public List<PromoterWithdrawalRecordViewModel> RecentWithdrawals { get; set; } = new();
        public bool HasPendingWithdrawal { get; set; }

        /// <summary>已上架商品速览（工作台左下角）</summary>
        public List<PromoterProductEntryDetailDto> ListedProducts { get; set; } = new();

        /// <summary>团内消费者速览（工作台左下角）</summary>
        public List<GroupC_CrmPCRelation> BoundCustomers { get; set; } = new();

        public string LevelName { get; set; } = string.Empty;
        /// <summary>当前佣金比例（百分数，= 平台设定的基础佣金比例）</summary>
        public decimal CurrentTierRate { get; set; }
        public decimal NextTierThreshold { get; set; }
        public string NextTierName { get; set; } = string.Empty;
        public decimal NextTierBonus { get; set; }
        public bool IsMaxTier { get; set; }
        public double TierProgressPercent { get; set; }

        public decimal TotalAsset => Promoter.CurrentBalance + Promoter.PendingBalance + Promoter.FrozenAmount;
        public int PendingCommissionCount { get; set; }
        public int SettledCommissionCount { get; set; }
        public decimal MonthCommissionTotal { get; set; }
    }

    public class PromoterPerformanceViewModel
    {
        public string PromoterId { get; set; } = string.Empty;
        public string PromoterName { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int TotalOrderCount { get; set; }
        public string LevelName { get; set; } = string.Empty;
        /// <summary>当前佣金比例（百分数，= 平台设定的基础佣金比例）</summary>
        public decimal CurrentTierRate { get; set; }
        public decimal NextTierThreshold { get; set; }
        public string NextTierName { get; set; } = string.Empty;
        public decimal NextTierBonus { get; set; }
        public bool IsMaxTier { get; set; }
        public double TierProgressPercent { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public List<PromoterTierStepViewModel> TierSteps { get; set; } = new();
        public List<PromoterMilestoneViewModel> Milestones { get; set; } = new();
    }

    public class PromoterTierStepViewModel
    {
        public string LevelName { get; set; } = string.Empty;
        public string SalesRange { get; set; } = string.Empty;
        /// <summary>达成该等级的里程碑奖励金额</summary>
        public decimal Bonus { get; set; }
        public decimal Threshold { get; set; }
        /// <summary>该等级对应的佣金比例（百分数，如 3 表示 3%）</summary>
        public decimal RatePercent { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsAchieved { get; set; }
    }

    public class PromoterMilestoneViewModel
    {
        public decimal Threshold { get; set; }
        public decimal BonusAmount { get; set; }
        public bool IsAchieved { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class PromoterCommissionItemViewModel
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public decimal CommBaseAmount { get; set; }
        public decimal CommBonusAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "secondary";
        public DateTime? CommSettlementDate { get; set; }
        public DateTime? SignedAt { get; set; }
        public decimal RefundedAmount { get; set; } = 0;
        public decimal TotalCommission => CommBaseAmount + CommBonusAmount - RefundedAmount;
    }

    public class PromoterCommissionsViewModel
    {
        public List<PromoterCommissionItemViewModel> Items { get; set; } = new();
        public string? StatusFilter { get; set; }
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int SettledCount { get; set; }
        public int RefundedCount { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal SettledAmount { get; set; }
        public decimal RefundedAmount { get; set; }
    }

    public class PromoterWithdrawalsViewModel
    {
        public string PromoterName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal FrozenAmount { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public int PendingCount { get; set; }
        public bool CanApply { get; set; }
        public string? BlockReason { get; set; }
        public WithdrawalApplyForm Form { get; set; } = new();
        public List<PromoterWithdrawalRecordViewModel> Records { get; set; } = new();
        public List<PromoterPayAccountItem> PayAccounts { get; set; } = new();
    }

    public class WithdrawalApplyForm
    {
        public string AccountPlatform { get; set; } = PromoterPayAccounts.WeChat;
        public string AccountInfo { get; set; } = string.Empty;
        public decimal ApplyAmount { get; set; }
    }

    public class PromoterPayAccountItem
    {
        public string Platform { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public string? AccountNo { get; set; }
        public bool IsBound => !string.IsNullOrWhiteSpace(AccountNo);
    }

    public class PromoterPayAccountBindModel
    {
        public string ReturnAction { get; set; } = "Withdrawals";
        public List<PromoterPayAccountItem> Accounts { get; set; } = new();
    }

    public class PromoterWithdrawalRecordViewModel
    {
        public string WithdrawalId { get; set; } = string.Empty;
        public decimal ApplyAmount { get; set; }
        public string AccountInfo { get; set; } = string.Empty;
        public DateTime ApplyTime { get; set; }
        public string AuditStatus { get; set; } = string.Empty;
        public string AuditStatusLabel { get; set; } = string.Empty;
        public string AuditStatusBadgeClass { get; set; } = "secondary";
        public string RejectReason { get; set; } = string.Empty;
        public DateTime? TransferTime { get; set; }
    }

    public class PromoterProfileViewModel
    {
        public GroupC_CrmPromoter Promoter { get; set; } = new();
        public string LevelName { get; set; } = string.Empty;
        public decimal CurrentTierRate { get; set; }
        public decimal TotalAsset { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "secondary";
        public List<PromoterPayAccountItem> PayAccounts { get; set; } = new();
    }
    public class PromoterBoundConsumersViewModel
    {
        public string PromoterId { get; set; } = string.Empty;
        public string PromoterName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public List<GroupC_CrmPCRelation> Items { get; set; } = new();
    }
}
