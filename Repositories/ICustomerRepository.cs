using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICustomerRepository
{
    Task<bool> PhoneExistsAsync(
        string phone,
        string? excludeCustomerId = null,
        IDbTransaction? transaction = null);

    Task<string> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null);

    Task<CrmCustomer?> GetByIdAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<CrmCustomer?> GetByIdForUpdateAsync(
        string customerId,
        IDbTransaction transaction);

    Task<List<CustomerAccount>> FindCustomerAccountsAsync(
        string? customerId = null,
        string? openId = null,
        string? phone = null,
        string? boundPromoterId = null,
        IDbTransaction? transaction = null);

    Task<bool> UpdateBindingAsync(
        string customerId,
        string? boundPromoterId,
        DateTime? bindExpireTime,
        int? growthValue = null,
        IDbTransaction? transaction = null);

    Task<List<CrmCustomer>> GetCustomersWithExpiredBindingsAsync(
        DateTime now,
        IDbTransaction? transaction = null);

    Task<bool> UpdateProfileAsync(
        CustomerProfileUpdateRequest request,
        IDbTransaction? transaction = null);

    Task UpdatePointsAsync(
        string customerId,
        int newPoints,
        IDbTransaction? transaction = null);

    Task UpdateTotalSpentAsync(
        string customerId,
        decimal addAmount,
        IDbTransaction? transaction = null);

    Task<bool> TrySubtractTotalSpentAsync(
        string customerId,
        decimal amount,
        IDbTransaction transaction);

    Task UpdateMemberLevelAsync(
        string customerId,
        string memberLevelId,
        IDbTransaction? transaction = null);

    Task<List<CrmUserAddress>> GetAddressesAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<CrmUserAddress?> GetAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null);

    Task<string> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null);

    Task<bool> UpdateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null);

    Task<bool> DeleteAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null);

    Task ClearDefaultAddressesAsync(
        string customerId,
        IDbTransaction? transaction = null);

    Task<bool> SetDefaultAddressAsync(
        string customerId,
        string addressId,
        IDbTransaction? transaction = null);
}
