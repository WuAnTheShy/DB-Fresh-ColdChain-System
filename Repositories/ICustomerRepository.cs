using System.Data;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface ICustomerRepository
{
    Task<CrmCustomer?> GetByIdAsync(
        int customerId,
        IDbTransaction? transaction = null);

    Task<CrmCustomer?> GetByIdForUpdateAsync(
        int customerId,
        IDbTransaction transaction);

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

    Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null);
}
