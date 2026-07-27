namespace FreshColdChain.Models;

/// <summary>
/// Crm_PointLogs - 积分流水 (每笔积分变动都记录)
/// </summary>
public class CrmPointLog
{
    public int PointLogId { get; set; }
    public int CustomerId { get; set; }
    public int ChangeAmount { get; set; }            // 变动量(正=获得,负=扣减)
    public int BalanceAfter { get; set; }            // 变动后余额
    public string ChangeType { get; set; } = string.Empty; // 如: ORDER_EARN / REFUND_DEDUCT
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}
