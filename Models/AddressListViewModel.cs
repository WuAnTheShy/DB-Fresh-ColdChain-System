namespace FreshColdChain.Models;

/// <summary>
/// 收货地址管理页面数据。
/// </summary>
public sealed class AddressListViewModel
{
    public string CustomerId { get; init; } = string.Empty;
    public IReadOnlyList<CrmUserAddress> Addresses { get; init; } = [];
}
