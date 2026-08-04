namespace DBFreshColdChain.Models.DTOs
{
    public class GroupC_UpdateCommisionRequest
    {
        public string PromoterId { get; set; } = string.Empty;       // 团长编号
        public decimal BaseCommissionRate { get; set; }              // 基础佣金比例(如 5.00 表示 5%)


    }
}
