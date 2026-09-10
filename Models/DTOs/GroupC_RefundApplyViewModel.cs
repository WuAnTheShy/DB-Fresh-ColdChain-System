namespace FreshColdChain.Models.DTOs
{
    // 消费者退款申请页视图模型：订单信息 + 该订单已有的退款申请记录
    public class GroupC_RefundApplyViewModel
    {
        public OrderDetailViewModel Order { get; set; } = new();
        public List<FinRefund> ExistingRefunds { get; set; } = new();
    }
}
