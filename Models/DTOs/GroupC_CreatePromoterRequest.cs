namespace FreshColdChain.Models.DTOs
{
    public class GroupC_CreatePromoterRequest
    {
        public string PromoterId { get; set; } = string.Empty;       //团长ID号
        public string PromoterName { get; set; } = string.Empty;     // 团长姓名
        public string Phone { get; set; } = string.Empty;            // 联系电话
        public string InviteCode { get; set; } = string.Empty;       // 邀请码


    }
}
