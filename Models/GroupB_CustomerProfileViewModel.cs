namespace FreshColdChain.Models;

/// <summary>
/// 消费者中心页面数据。
/// </summary>
public sealed class CustomerProfileViewModel
{
    public required CrmCustomer Customer { get; init; }
    public CrmMemberLevel? MemberLevel { get; init; }
    public IReadOnlyList<CrmUserAddress> Addresses { get; init; } = [];
}
