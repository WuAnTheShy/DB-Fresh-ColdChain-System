namespace FreshColdChain.Models
{
    /// <summary>
    /// C组阶梯奖励政策：团长累计销售额每跨过一档，即发放对应奖励佣金（一单跨多档可叠加）；
    /// 退款导致累计销售额向下跌破档位时，按同一阶梯表撤销对应奖励佣金。
    /// 一段结算（CommissionService）与退款回滚（RefundService）共用此表，保证发放/撤销对称。
    /// </summary>
    public static class GroupC_CommissionBonusPolicy
    {
        /// <summary>阶梯表：累计销售额阈值 → 跨过该档发放的奖励佣金</summary>
        public static readonly (decimal Threshold, decimal Bonus)[] Tiers =
        {
            (1000m, 50m), (2000m, 50m),
            (3000m, 150m), (4000m, 150m), (5000m, 750m),
            (6000m, 150m), (7000m, 150m),
            (8000m, 450m), (9000m, 450m), (10000m, 750m)
        };

        /// <summary>累计销售额从 from 提升到 to（to &gt; from）时，所有被跨过档位的奖励总和</summary>
        public static decimal CalculateCrossedBonus(decimal from, decimal to)
        {
            decimal bonus = 0;
            foreach (var (threshold, tierBonus) in Tiers)
            {
                if (from < threshold && to >= threshold)
                    bonus += tierBonus;
            }
            return bonus;
        }

        /// <summary>累计销售额从 from 跌落到 to（to &lt; from）时，所有被跌破档位需撤销的奖励总和</summary>
        public static decimal CalculateRollbackBonus(decimal from, decimal to)
        {
            // 向下跌破档位 = 反方向跨过档位，与发放逻辑对称
            return CalculateCrossedBonus(to, from);
        }
    }
}
