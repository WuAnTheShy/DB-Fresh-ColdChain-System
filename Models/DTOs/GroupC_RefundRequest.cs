namespace FreshColdChain.Models.DTOs
{
    public class GroupC_RefundRequest
    {
        public string? OrderId { get; set; } = string.Empty;          // 订单编号 
        public string? DetailId { get; set; } = string.Empty;         // 订单明细编号 
        public string? ProductID {  get; set; } = string.Empty;       // 退款商品编号
        public int RefundQty { get; set; }                            // 退款数量
        public string? LiabilityType { get; set; } = "Customer";      // 责任归属: Supplier / Platform / Customer / Logistics
        public string? Remark { get; set; } = string.Empty;           // 备注
        public List<GroupC_RefundItemRequest> Items { get; set; } = []; // 统一入口选择的一个或多个退货商品
    }

    public class GroupC_RefundItemRequest
    {
        public string ProductID { get; set; } = string.Empty;
        public int RefundQty { get; set; }
    }
}
