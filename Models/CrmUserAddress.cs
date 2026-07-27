namespace FreshColdChain.Models;

/// <summary>
/// Crm_UserAddresses - 用户收货地址
/// </summary>
public class CrmUserAddress
{
    public int AddressId { get; set; }
    public int CustomerId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string DetailAddress { get; set; } = string.Empty;
    public int IsDefault { get; set; }             // 0/1 是否默认地址
    public DateTime CreatedAt { get; set; }
}
