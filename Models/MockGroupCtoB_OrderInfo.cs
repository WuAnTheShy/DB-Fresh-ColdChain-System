using DBFreshColdChain.Models;
namespace DBFreshColdChain.Models
{
    public class MockGroupCtoB_OrderInfo
    {
        public string? OrderId { get; set; }
        public string? OrderNo { get; set; }
        public string? CustomerId { get; set; }
        public string? PromoterId { get; set; }
        public string? AddressId { get; set; }
        public decimal GoodsAmount { get; set; } = 0;
        public decimal FreightAmount { get; set; } = 0;
        public decimal CouponAmount { get; set; } = 0;
        public decimal FinalAmount { get; set; } = 0;
        public string? Status { get; set; }
        public decimal CommBaseAmount { get; set; } = 0;
        public decimal CommBonusAmount { get; set; } = 0;
        public DateTime CommSettlementDate { get; set; }


    }

}
