using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 消费者数据访问层 - Crm_Customers, Crm_UserAddresses
/// </summary>
public class CustomerRepository : BaseRepository
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
