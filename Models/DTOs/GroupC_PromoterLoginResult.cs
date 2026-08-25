namespace FreshColdChain.Models.DTOs
{
    public class GroupC_PromoterLoginResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PromoterId { get; set; } = string.Empty;
        public string PromoterName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalSales { get; set; }
        public string InviteCode { get; set; } = string.Empty;
    }

}
