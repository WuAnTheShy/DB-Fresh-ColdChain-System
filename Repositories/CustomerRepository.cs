using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 消费者数据访问层 - Crm_Customers, Crm_UserAddresses
/// </summary>
public class CustomerRepository : BaseRepository, ICustomerRepository
{
    public CustomerRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<CrmCustomer?> GetByIdAsync(int customerId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId },
                transaction));
    }

    /// <summary>锁定消费者行，防止并发订单覆盖积分余额</summary>
    public async Task<CrmCustomer?> GetByIdForUpdateAsync(
        int customerId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE CustomerId = :CustomerId FOR UPDATE",
                new { CustomerId = customerId },
                transaction));
    }

    /// <summary>校验收货地址属于当前消费者</summary>
    public async Task<bool> AddressBelongsToCustomerAsync(
        int addressId,
        int customerId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM Crm_UserAddresses
                  WHERE AddressId = :AddressId AND CustomerId = :CustomerId",
                new { AddressId = addressId, CustomerId = customerId },
                transaction);
            return count > 0;
        });
    }

    public async Task UpdatePointsAsync(int customerId, int newPoints, IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                "UPDATE Crm_Customers SET Points = :Points, UpdatedAt = SYSDATE WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId, Points = newPoints },
                transaction);
        });
    }

    public async Task UpdateTotalSpentAsync(int customerId, decimal addAmount, IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET TotalSpent = TotalSpent + :Amount, UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId, Amount = addAmount },
                transaction);
        });
    }

    public async Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmUserAddress>(
                "SELECT * FROM Crm_UserAddresses WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId },
                transaction)).ToList());
    }
}
