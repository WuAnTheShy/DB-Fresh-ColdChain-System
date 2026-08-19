namespace DBFreshColdChain.Models
{

    public class CommissionRecord
    {

        public string RecordId { get; set; } = string.Empty;


        public string PromoterId { get; set; } = string.Empty;


        public string OrderId { get; set; } = string.Empty;

     
        public decimal FinalAmount { get; set; }


        public decimal CommBaseAmount { get; set; }


        public decimal CommBonusAmount { get; set; }


        public decimal TotalCommission { get; set; }


        public DateTime? SignDate { get; set; }


        public DateTime? ExpectedSettleDate { get; set; }


        public string Status { get; set; } = "Pending";
        public decimal RefundedAmount { get; set; } = 0;

    }
}