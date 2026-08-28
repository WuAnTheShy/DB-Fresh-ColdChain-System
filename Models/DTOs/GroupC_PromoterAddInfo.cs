namespace FreshColdChain.Models.DTOs
{
    public class GroupC_PromoterAddInfo
    {
        public string PromoterName { get; set; } = string.Empty;
        public string LoginAccount { get; set; } = string.Empty;
        public string LoginPassword { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal? BaseCommissionRate { get; set; }
    }
}
