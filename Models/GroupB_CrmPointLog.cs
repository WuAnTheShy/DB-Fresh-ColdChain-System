namespace FreshColdChain.Models;

// Crm_PointLogs - 积分流水 (每笔积分变动都记录)
public class CrmPointLog
{
    public string PointLogId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public int ChangeAmount { get; set; }            // 变动量(正=获得,负=扣减)
    public int BalanceAfter { get; set; }            // 变动后余额
    public string ChangeType { get; set; } = string.Empty; // 如: ORDER_EARN / REFUND_DEDUCT
    public string? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}
