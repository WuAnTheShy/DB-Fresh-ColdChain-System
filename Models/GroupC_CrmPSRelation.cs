namespace FreshColdChainSystem.Models
{
    public class GroupC_CrmPSRelation
    {
        public string PromoterId { get; set; } = string.Empty;
        public string SupplierId { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }
    }

}
