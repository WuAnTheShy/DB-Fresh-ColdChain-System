namespace DBFreshColdChain.Models.CrossGroup
{
    public class MockGroupCtoB_OrderDetailInfo
    {
        public string? DetailId { get; set; }
        public string? OrderId { get; set; }
        public string? DeliveryId { get; set; }
        public string? ProductId { get; set; }
        public decimal? Quantity { get; set; } = 0;
        public decimal UnitPrice { get; set; } = 0;
        public decimal SubTotal { get; set; } = 0;


    }

}
