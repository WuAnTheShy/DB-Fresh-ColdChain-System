namespace FreshColdChain.Models.DTOs
{
    public class GroupC_PromoterLoginResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string PromoterId { get; set; }
        public string PromoterName { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalSales { get; set; }
        public string InviteCode { get; set; }
    }

}
