namespace FreshColdChain.Models.DTOs
{
    public class GroupC_UpdateCommisionRequest
    {
        public string PromoterId { get; set; } = string.Empty;       // 团长编号
        public decimal BaseCommissionRate { get; set; }              // 基础佣金比例(小数，如 0.03 表示 3%，与 CRM_PROMOTERS.BASECOMMISSIONRATE 存储一致)


    }
}
