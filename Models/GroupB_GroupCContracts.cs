namespace FreshColdChain.Models;

// B 组对外提供的消费者账户只读快照。
public sealed class CustomerAccount
{
    public string CustomerID { get; init; } = string.Empty;
    public string? OpenID { get; init; }
    public string Phone { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int GrowthValue { get; init; }
    public string? BoundPromoterID { get; init; }
    public DateTime? BindExpireTime { get; init; }
}
