namespace DBFreshColdChain.Models
{
    public class CreatePromoterRequest
    {
        public string PromoterId { get; set; } = string.Empty;       //团长ID号
        public string PromoterName { get; set; }                     // 团长姓名
        public string Phone { get; set; }                            // 联系电话
        public string InviteCode { get; set; }                       // 邀请码
            

    }
}
