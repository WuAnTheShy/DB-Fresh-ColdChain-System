namespace FreshColdChain.Models
{
    // CRM_PCR - 团长与消费者的多对多绑定关系。
    // 表字段：CONSUMERID、PROMOTERID，后续字段由 C 组再补。
    public class GroupC_CrmPCRelation
    {
        public string CustomerId { get; set; } = string.Empty;
        public string PromoterId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
    }
}
