namespace FreshColdChain.Models
{
    /// <summary>
    /// 等级佣金政策：佣金比例与团长等级挂钩（等级越高比例越高）。
    /// 等级由累计销售额决定，阈值与 GroupC_CommissionBonusPolicy 跨档奖励阈值保持一致。
    /// </summary>
    public static class GroupC_LevelCommissionPolicy
    {
        // 等级阈值（累计销售额）
        public const decimal SilverThreshold = 1000m;   // 白银
        public const decimal GoldThreshold = 3000m;     // 黄金
        public const decimal DiamondThreshold = 5000m;  // 钻石

        // 等级对应佣金比例（小数，如 0.03 表示 3%）
        public const decimal BronzeRate = 0.03m;   // 青铜 3%
        public const decimal SilverRate = 0.04m;   // 白银 4%
        public const decimal GoldRate = 0.05m;     // 黄金 5%
        public const decimal DiamondRate = 0.08m;  // 钻石 8%

        /// <summary>按累计销售额返回当前等级对应的佣金比例</summary>
        public static decimal ResolveRate(decimal totalSales)
        {
            if (totalSales >= DiamondThreshold) return DiamondRate;
            if (totalSales >= GoldThreshold) return GoldRate;
            if (totalSales >= SilverThreshold) return SilverRate;
            return BronzeRate;
        }
    }
}
