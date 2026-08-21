using FreshColdChain.Models;
using FreshColdChain.Models.ViewModels;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Server.HttpSys;

namespace FreshColdChain.Services
{
    /// <summary>
    /// 团长端页面数据提供者。优先读库，失败时回退演示数据，便于前端联调。
    /// </summary>
    public class PromoterPortalDataProvider
    {
        private readonly IPromoterRepository? _promoterRepository;
        private readonly ICommissionRepository _icommissionRepository;
        private static readonly Dictionary<string, GroupC_CrmPromoter> DemoPromoters = new();
        private static readonly Dictionary<string, List<PromoterWithdrawalRecordViewModel>> DemoWithdrawals = new();

        private static readonly Dictionary<decimal, decimal> MilestoneBonuses = new()
        {
            [1000m] = 30m,
            [3000m] = 80m,
            [5000m] = 150m
        };

        public PromoterPortalDataProvider(IPromoterRepository? promoterRepository, ICommissionRepository icommissionRepository)
        {
            _promoterRepository = promoterRepository;
            _icommissionRepository = icommissionRepository;
        }

        public GroupC_CrmPromoter GetPromoter(string promoterId)
        {
            if (_promoterRepository != null)
            {
                try
                {
                    var record = _promoterRepository.GroupC_FindPromoterRecordAsync(promoterId).GetAwaiter().GetResult();
                    if (record != null)
                        return record;
                }
                catch
                {
                    // 数据库不可用时使用演示数据
                }
            }

            if (!DemoPromoters.TryGetValue(promoterId, out var demo))
            {
                demo = BuildDemoPromoter(promoterId);
                DemoPromoters[promoterId] = demo;
            }
            return demo;
        }

        public List<PromoterCommissionItemViewModel> GetCommissions(string promoterId, string? statusFilter = null)
        {
            var records = _icommissionRepository.GetByPromoterId(promoterId, statusFilter);

            return records.Select(r => new PromoterCommissionItemViewModel
            {
                OrderId = r.OrderId,
                FinalAmount = r.FinalAmount,
                CommBaseAmount = r.CommBaseAmount,
                CommBonusAmount = r.CommBonusAmount,
                Status = r.Status,
                StatusLabel = GetStatusLabel(r.Status),          // 辅助方法转换状态文字
                StatusBadgeClass = GetStatusBadgeClass(r.Status), // 辅助方法转换标签颜色
                SignedAt = r.SignDate,
                RefundedAmount = r.RefundedAmount,
                CommSettlementDate  = r.ExpectedSettleDate,
            }).ToList();
        }

        public PromoterCommissionsViewModel BuildCommissions(string promoterId, string? statusFilter = null)
        {
            var promoter = _promoterRepository.GroupC_FindPromoterRecord(promoterId);
            if (promoter == null)
                return new PromoterCommissionsViewModel(); // 为空则返回空页面
            var all = GetCommissions(promoterId);
            var filtered = string.IsNullOrWhiteSpace(statusFilter)
                ? all
                : all.Where(x => x.Status == statusFilter).ToList();

            return new PromoterCommissionsViewModel
            {
                StatusFilter = statusFilter,
                Items = filtered,
                TotalCount = all.Count,
                PendingCount = all.Count(x => x.Status == "Pending"),
                SettledCount = all.Count(x => x.Status == "Settled"),
                RefundedCount = all.Count(x => x.Status == "Refunded"),
                PendingAmount = promoter.PendingBalance,
                SettledAmount = promoter.CurrentBalance,
                RefundedAmount = all.Sum(x => x.RefundedAmount)
            };
        }

        public List<PromoterWithdrawalRecordViewModel> GetWithdrawals(string promoterId)
        {
            if (DemoWithdrawals.TryGetValue(promoterId, out var records))
                return records.OrderByDescending(x => x.ApplyTime).ToList();
            return BuildDemoWithdrawalRecords(promoterId);
        }

        public PromoterWithdrawalsViewModel BuildWithdrawals(string promoterId, WithdrawalApplyForm? form = null)
        {
            var promoter = GetPromoter(promoterId);
            var records = GetWithdrawals(promoterId);
            var hasPending = records.Any(x => x.AuditStatus == "Pending");
            string? blockReason = null;

            if (IsDisabled(promoter.Status))
                blockReason = "账号已禁用，暂不可申请提现。";
            else if (hasPending)
                blockReason = "您有一笔提现申请正在审核中，请等待处理后再提交。";
            else if (promoter.CurrentBalance <= 0)
                blockReason = "当前可提现余额不足。";

            return new PromoterWithdrawalsViewModel
            {
                PromoterName = promoter.PromoterName,
                CurrentBalance = promoter.CurrentBalance,
                FrozenAmount = promoter.FrozenAmount,
                TotalWithdrawn = records.Where(r => r.AuditStatus == "Approved").Sum(r => r.ApplyAmount),
                PendingCount = records.Count(r => r.AuditStatus == "Pending"),
                CanApply = !IsDisabled(promoter.Status) && !hasPending && promoter.CurrentBalance > 0,
                BlockReason = blockReason,
                Form = form ?? new WithdrawalApplyForm(),
                Records = records
            };
        }

        public bool HasPendingWithdrawal(string promoterId)
        {
            return GetWithdrawals(promoterId).Any(x => x.AuditStatus == "Pending");
        }

        public (bool Success, string Message) ApplyWithdrawal(string promoterId, WithdrawalApplyForm form)
        {
            var promoter = GetPromoter(promoterId);
            if (IsDisabled(promoter.Status))
                return (false, "账号已禁用，无法申请提现。");
            if (HasPendingWithdrawal(promoterId))
                return (false, "您有一笔提现申请正在审核中，请等待处理后再提交。");
            if (form.ApplyAmount <= 0)
                return (false, "提现金额必须大于 0。");
            if (form.ApplyAmount > promoter.CurrentBalance)
                return (false, "提现金额不能超过可提现余额。");
            if (string.IsNullOrWhiteSpace(form.AccountInfo))
                return (false, "请填写收款账户信息。");

            if (!DemoWithdrawals.ContainsKey(promoterId))
                DemoWithdrawals[promoterId] = BuildDemoWithdrawalRecords(promoterId);

            var record = new PromoterWithdrawalRecordViewModel
            {
                WithdrawalId = $"WD{DateTime.Now:yyyyMMddHHmmss}",
                ApplyAmount = form.ApplyAmount,
                AccountInfo = $"{MapPlatformLabel(form.AccountPlatform)}：{form.AccountInfo}",
                ApplyTime = DateTime.Now,
                AuditStatus = "Pending",
                AuditStatusLabel = "待审核",
                AuditStatusBadgeClass = "warning"
            };
            DemoWithdrawals[promoterId].Insert(0, record);
            promoter.CurrentBalance -= form.ApplyAmount;
            promoter.FrozenAmount += form.ApplyAmount;
            return (true, "提现申请已提交，请等待平台审核。");
        }

        public PromoterPerformanceViewModel BuildPerformance(GroupC_CrmPromoter promoter)
        {
            var (currentRate, nextThreshold, nextRate, isMax) = ResolveTier(promoter.TotalSales);
            var progressPct = isMax ? 100.0
                : nextThreshold > 0
                    ? Math.Min(100, (double)(promoter.TotalSales / nextThreshold * 100))
                    : 0;

            var tierDefs = new[]
            {
                ("青铜团长", "< ¥1,000", 3m, 0m),
                ("白银团长", "¥1,000 ~ ¥3,000", 4m, 1000m),
                ("黄金团长", "¥3,000 ~ ¥5,000", 5m, 3000m),
                ("钻石团长", "> ¥5,000", 8m, 5000m)
            };
            var levelName = ResolveLevelName(promoter.TotalSales);

            return new PromoterPerformanceViewModel
            {
                PromoterId = promoter.PromoterId,
                PromoterName = promoter.PromoterName,
                TotalSales = promoter.TotalSales,
                TotalOrderCount = promoter.TotalOrderCount,
                LevelName = levelName,
                CurrentTierRate = currentRate,
                NextTierThreshold = nextThreshold,
                NextTierRate = nextRate,
                IsMaxTier = isMax,
                TierProgressPercent = progressPct,
                PendingBalance = promoter.PendingBalance,
                CurrentBalance = promoter.CurrentBalance,
                TierSteps = tierDefs.Select(t => new PromoterTierStepViewModel
                {
                    LevelName = t.Item1,
                    SalesRange = t.Item2,
                    Rate = t.Item3,
                    Threshold = t.Item4,
                    IsCurrent = t.Item1 == levelName,
                    IsAchieved = t.Item4 switch
                    {
                        0m => true,
                        1000m => promoter.TotalSales >= 1000,
                        3000m => promoter.TotalSales >= 3000,
                        5000m => promoter.TotalSales >= 5000,
                        _ => false
                    }
                }).ToList(),
                Milestones = MilestoneBonuses.Select(m => new PromoterMilestoneViewModel
                {
                    Threshold = m.Key,
                    BonusAmount = m.Value,
                    IsAchieved = promoter.TotalSales >= m.Key,
                    Label = $"首次达到 ¥{m.Key:N0}"
                }).ToList()
            };
        }

        public PromoterProfileViewModel BuildProfile(string promoterId)
        {
            var promoter = GetPromoter(promoterId);
            var performance = BuildPerformance(promoter);
            var (statusLabel, statusClass) = ResolveStatusDisplay(promoter.Status);

            return new PromoterProfileViewModel
            {
                Promoter = promoter,
                LevelName = performance.LevelName,
                CurrentTierRate = performance.CurrentTierRate,
                TotalAsset = promoter.CurrentBalance + promoter.PendingBalance + promoter.FrozenAmount,
                StatusLabel = statusLabel,
                StatusBadgeClass = statusClass
            };
        }

        public PromoterDashboardViewModel BuildDashboard(string promoterId)
        {
            var promoter = GetPromoter(promoterId);
            var commissions = GetCommissions(promoterId);
            var performance = BuildPerformance(promoter);

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthCommissions = commissions
                .Where(c => c.SignedAt >= monthStart && c.Status != "Refunded")
                .Sum(c => c.TotalCommission);

            return new PromoterDashboardViewModel
            {
                Promoter = promoter,
                RecentCommissions = commissions.Take(5).ToList(),
                RecentWithdrawals = GetWithdrawals(promoterId).Take(3).ToList(),
                HasPendingWithdrawal = HasPendingWithdrawal(promoterId),
                LevelName = performance.LevelName,
                CurrentTierRate = performance.CurrentTierRate,
                NextTierThreshold = performance.NextTierThreshold,
                NextTierRate = performance.NextTierRate,
                IsMaxTier = performance.IsMaxTier,
                TierProgressPercent = performance.TierProgressPercent,
                PendingCommissionCount = commissions.Count(c => c.Status == "Pending"),
                SettledCommissionCount = commissions.Count(c => c.Status == "Settled"),
                MonthCommissionTotal = monthCommissions
            };
        }

        public static string ResolveLevelName(decimal totalSales)
        {
            if (totalSales >= 5000) return "钻石团长";
            if (totalSales >= 3000) return "黄金团长";
            if (totalSales >= 1000) return "白银团长";
            return "青铜团长";
        }

        private static bool IsDisabled(string status) =>
            status is "Disabled" or "Disable" or "Frozen";

        private static (string Label, string BadgeClass) ResolveStatusDisplay(string status) => status switch
        {
            "Enabled" or "Enable" or "Active" => ("正常", "success"),
            "Pending" => ("待审核", "warning"),
            "Disabled" or "Disable" or "Frozen" => ("已禁用", "danger"),
            _ => (status, "secondary")
        };

        private static (decimal CurrentRate, decimal NextThreshold, decimal NextRate, bool IsMax) ResolveTier(decimal totalSales)
        {
            if (totalSales >= 5000) return (8m, 0, 8m, true);
            if (totalSales >= 3000) return (5m, 5000, 8m, false);
            if (totalSales >= 1000) return (4m, 3000, 5m, false);
            return (3m, 1000, 4m, false);
        }

        private static string MapPlatformLabel(string platform) => platform switch
        {
            "Alipay" => "支付宝",
            "BankCard" => "银行卡",
            _ => "微信"
        };

        private static GroupC_CrmPromoter BuildDemoPromoter(string promoterId) => new()
        {
            PromoterId = promoterId,
            PromoterName = "演示团长",
            Phone = "13800138000",
            InviteCode = "TEAM2026",
            BaseCommissionRate = 0.05m,
            CurrentBalance = 1280.50m,
            PendingBalance = 356.00m,
            FrozenAmount = 200.00m,
            TotalSales = 4680.00m,
            TotalOrderCount = 42,
            Status = "Enabled",
            RegisterTime = new DateTime(2026, 3, 15),
            LastSettlementTime = new DateTime(2026, 8, 1)
        };


        private static List<PromoterWithdrawalRecordViewModel> BuildDemoWithdrawalRecords(string promoterId) =>
        [
            new()
            {
                WithdrawalId = "WD202608001",
                ApplyAmount = 500.00m,
                AccountInfo = "微信：demo_wx_001",
                ApplyTime = new DateTime(2026, 8, 5, 14, 30, 0),
                AuditStatus = "Approved",
                AuditStatusLabel = "已通过",
                AuditStatusBadgeClass = "info",
                TransferTime = new DateTime(2026, 8, 6, 10, 0, 0)
            },
            new()
            {
                WithdrawalId = "WD202607015",
                ApplyAmount = 200.00m,
                ApplyTime = new DateTime(2026, 7, 20, 9, 15, 0),
                AccountInfo = "支付宝：demo_alipay_001",
                AuditStatus = "Rejected",
                AuditStatusLabel = "已驳回",
                AuditStatusBadgeClass = "danger",
                RejectReason = "收款账户信息与实名不一致"
            }
        ];



        private string GetStatusLabel(string status)
        {
            return status switch
            {
                "Pending" => "待结算",
                "Settled" => "可提现",
                "Refunded" => "已退款扣减",
                _ => status // 或者 "未知"
            };
        }
        private string GetStatusBadgeClass(string status)
        {
            return status switch
            {
                "Pending" => "warning",
                "Settled" => "success",
                "Refunded" => "danger",
                _ => "secondary"
            };
        }
    }
}
