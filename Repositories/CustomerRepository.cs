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

    public async Task<bool> PhoneExistsAsync(
        string phone,
        int? excludeCustomerId = null,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM Crm_Customers
                  WHERE Phone = :Phone
                    AND (:ExcludeCustomerId IS NULL OR CustomerId <> :ExcludeCustomerId)",
                new { Phone = phone, ExcludeCustomerId = excludeCustomerId },
                transaction);
            return count > 0;
        });
    }

    public async Task<int> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Crm_Customers (
                CustomerName, Phone, Email, PasswordHash, PromoterId,
                MemberLevelId, TotalSpent, Points, CreatedAt)
            VALUES (
                :CustomerName, :Phone, :Email, :PasswordHash, :PromoterId,
                :MemberLevelId, 0, 0, SYSDATE)
            RETURNING CustomerId INTO :CustomerId";

        var parameters = new DynamicParameters(customer);
        parameters.Add(
            "CustomerId",
            dbType: DbType.Int32,
            direction: ParameterDirection.Output);

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, parameters, transaction);
            return parameters.Get<int>("CustomerId");
        });
    }

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

    /// <summary>只更新消费者允许自行维护的资料</summary>
    public async Task<bool> UpdateProfileAsync(
        CustomerProfileUpdateRequest request,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET CustomerName = :CustomerName,
                      Phone = :Phone,
                      Email = :Email,
                      UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                request,
                transaction);
            return affected == 1;
        });
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

    public async Task UpdateMemberLevelAsync(
        int customerId,
        int memberLevelId,
        IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET MemberLevelId = :MemberLevelId, UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId, MemberLevelId = memberLevelId },
                transaction);
        });
    }

    public async Task<List<CrmUserAddress>> GetAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmUserAddress>(
                @"SELECT * FROM Crm_UserAddresses
                  WHERE CustomerId = :CustomerId
                  ORDER BY IsDefault DESC, CreatedAt DESC, AddressId DESC",
                new { CustomerId = customerId },
                transaction)).ToList());
    }

    public async Task<CrmUserAddress?> GetAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmUserAddress>(
                @"SELECT * FROM Crm_UserAddresses
                  WHERE CustomerId = :CustomerId AND AddressId = :AddressId",
                new { CustomerId = customerId, AddressId = addressId },
                transaction));
    }

    public async Task<int> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Crm_UserAddresses (
                CustomerId, ReceiverName, Phone, Province, City, District,
                DetailAddress, IsDefault, CreatedAt)
            VALUES (
                :CustomerId, :ReceiverName, :Phone, :Province, :City, :District,
                :DetailAddress, :IsDefault, SYSDATE)
            RETURNING AddressId INTO :AddressId";

        var parameters = new DynamicParameters(address);
        parameters.Add(
            "AddressId",
            dbType: DbType.Int32,
            direction: ParameterDirection.Output);

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, parameters, transaction);
            return parameters.Get<int>("AddressId");
        });
    }

    public async Task<bool> UpdateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_UserAddresses
                  SET ReceiverName = :ReceiverName,
                      Phone = :Phone,
                      Province = :Province,
                      City = :City,
                      District = :District,
                      DetailAddress = :DetailAddress,
                      IsDefault = :IsDefault
                  WHERE AddressId = :AddressId AND CustomerId = :CustomerId",
                address,
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> DeleteAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"DELETE FROM Crm_UserAddresses
                  WHERE AddressId = :AddressId AND CustomerId = :CustomerId",
                new { CustomerId = customerId, AddressId = addressId },
                transaction);
            return affected == 1;
        });
    }

    public async Task ClearDefaultAddressesAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                @"UPDATE Crm_UserAddresses
                  SET IsDefault = 0
                  WHERE CustomerId = :CustomerId AND IsDefault = 1",
                new { CustomerId = customerId },
                transaction);
        });
    }

    public async Task<bool> SetDefaultAddressAsync(
        int customerId,
        int addressId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_UserAddresses
                  SET IsDefault = 1
                  WHERE AddressId = :AddressId AND CustomerId = :CustomerId",
                new { CustomerId = customerId, AddressId = addressId },
                transaction);
            return affected == 1;
        });
    }
}
