using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICustomerRepository
{
    Task<bool> PhoneExistsAsync(
        string phone,
        int? excludeCustomerId = null,
        IDbTransaction? transaction = null);

    Task<int> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null);

    Task<CrmCustomer?> GetByIdAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<CrmCustomer?> GetByIdForUpdateAsync(
        int customerId,
        IDbTransaction transaction);

    Task<bool> UpdateProfileAsync(
        CustomerProfileUpdateRequest request,
        IDbTransaction? transaction = null);

    Task<bool> AddressBelongsToCustomerAsync(
        int addressId,
        int customerId,
        IDbTransaction transaction);

    Task UpdatePointsAsync(
        int customerId,
        int newPoints,
        IDbTransaction? transaction = null);

    Task UpdateTotalSpentAsync(
        int customerId,
        decimal addAmount,
        IDbTransaction? transaction = null);

    Task UpdateMemberLevelAsync(
        int customerId,
        int memberLevelId,
        IDbTransaction? transaction = null);

    Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<CrmUserAddress?> GetAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null);

    Task<int> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null);

    Task<bool> UpdateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null);

    Task<bool> DeleteAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null);

    Task ClearDefaultAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<bool> SetDefaultAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null);
}
