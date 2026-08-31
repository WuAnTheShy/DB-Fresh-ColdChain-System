using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 消费者数据访问层 - Crm_Customers, Crm_UserAddresses
/// </summary>
public class CustomerRepository : B_BaseRepository, ICustomerRepository
{
    public CustomerRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<bool> PhoneExistsAsync(
        string phone,
        string? excludeCustomerId = null,
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

    public async Task<string> CreateCustomerAsync(
        CrmCustomer customer,
        IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Crm_Customers (
                CustomerId, CustomerName, Phone, Email, Avatar, PasswordHash, OpenId, PromoterId,
                MemberLevelId, TotalSpent, Points, GrowthValue, BindExpireTime, CreatedAt)
            VALUES (
                :CustomerId, :CustomerName, :Phone, :Email, :Avatar, :PasswordHash, :OpenId, :PromoterId,
                :MemberLevelId, 0, 0, :GrowthValue, :BindExpireTime, SYSDATE)";

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, customer, transaction);
            return customer.CustomerId;
        });
    }

    public async Task<CrmCustomer?> GetByIdAsync(string customerId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId },
                transaction));
    }

    public async Task<CrmCustomer?> GetByPhoneAsync(
        string phone,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE Phone = :Phone",
                new { Phone = phone },
                transaction));
    }

    public async Task<CrmCustomer?> GetByPhoneForUpdateAsync(
        string phone,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE Phone = :Phone FOR UPDATE",
                new { Phone = phone },
                transaction));
    }

    public async Task<bool> UpdatePasswordHashAsync(
        string customerId,
        string passwordHash,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET PasswordHash = :PasswordHash, UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId, PasswordHash = passwordHash },
                transaction);
            return affected == 1;
        });
    }

    /// <summary>锁定消费者行，防止并发订单覆盖积分余额</summary>
    public async Task<CrmCustomer?> GetByIdForUpdateAsync(
        string customerId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmCustomer>(
                "SELECT * FROM Crm_Customers WHERE CustomerId = :CustomerId FOR UPDATE",
                new { CustomerId = customerId },
                transaction));
    }

    public async Task<List<CustomerAccount>> FindCustomerAccountsAsync(
        string? customerId = null,
        string? openId = null,
        string? phone = null,
        string? boundPromoterId = null,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CustomerAccount>(
                @"SELECT CustomerId AS CustomerID,
                         OpenId,
                         Phone,
                         Points AS PointsBalance,
                         GrowthValue,
                         PromoterId AS BoundPromoterID,
                         BindExpireTime
                  FROM Crm_Customers
                  WHERE (:CustomerId IS NULL OR CustomerId = :CustomerId)
                    AND (:OpenId IS NULL OR OpenId = :OpenId)
                    AND (:Phone IS NULL OR Phone = :Phone)
                    AND (:BoundPromoterId IS NULL OR PromoterId = :BoundPromoterId)",
                new
                {
                    CustomerId = customerId,
                    OpenId = openId,
                    Phone = phone,
                    BoundPromoterId = boundPromoterId
                },
                transaction)).ToList());
    }

    public async Task<bool> UpdateBindingAsync(
        string customerId,
        string? boundPromoterId,
        DateTime? bindExpireTime,
        int? growthValue = null,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET PromoterId = :PromoterId,
                      BindExpireTime = :BindExpireTime,
                      GrowthValue = NVL(:GrowthValue, GrowthValue),
                      UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                new
                {
                    CustomerId = customerId,
                    PromoterId = boundPromoterId,
                    BindExpireTime = bindExpireTime,
                    GrowthValue = growthValue
                },
                transaction);
            return affected == 1;
        });
    }

    public async Task<List<CrmCustomer>> GetCustomersWithExpiredBindingsAsync(
        DateTime now,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmCustomer>(
                @"SELECT * FROM Crm_Customers
                  WHERE BindExpireTime IS NOT NULL
                    AND BindExpireTime <= :Now",
                new { Now = now },
                transaction)).ToList());
    }

    public async Task<List<CrmCustomer>> GetAllCustomersAsync(IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmCustomer>("SELECT * FROM Crm_Customers", transaction: transaction)).ToList());
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
                      Avatar = :Avatar,
                      UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId",
                request,
                transaction);
            return affected == 1;
        });
    }

    public async Task UpdatePointsAsync(string customerId, int newPoints, IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                "UPDATE Crm_Customers SET Points = :Points, UpdatedAt = SYSDATE WHERE CustomerId = :CustomerId",
                new { CustomerId = customerId, Points = newPoints },
                transaction);
        });
    }

    public async Task UpdateTotalSpentAsync(string customerId, decimal addAmount, IDbTransaction? transaction = null)
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

    public Task SetTotalSpentAsync(string customerId, decimal totalSpent, IDbTransaction transaction) =>
        WithConnectionAsync(transaction, connection => connection.ExecuteAsync(
            "UPDATE Crm_Customers SET TotalSpent = :TotalSpent, UpdatedAt = SYSDATE WHERE CustomerId = :CustomerId",
            new { CustomerId = customerId, TotalSpent = totalSpent }, transaction));

    public async Task<bool> TrySubtractTotalSpentAsync(
        string customerId,
        decimal amount,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Crm_Customers
                  SET TotalSpent = TotalSpent - :Amount, UpdatedAt = SYSDATE
                  WHERE CustomerId = :CustomerId AND TotalSpent >= :Amount",
                new { CustomerId = customerId, Amount = amount },
                transaction);
            return affected == 1;
        });
    }

    public async Task UpdateMemberLevelAsync(
        string customerId,
        string memberLevelId,
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
        string customerId,
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
        string customerId,
        string addressId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmUserAddress>(
                @"SELECT * FROM Crm_UserAddresses
                  WHERE CustomerId = :CustomerId AND AddressId = :AddressId",
                new { CustomerId = customerId, AddressId = addressId },
                transaction));
    }

    public async Task<string> CreateAddressAsync(
        CrmUserAddress address,
        IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Crm_UserAddresses (
                AddressId, CustomerId, ReceiverName, Phone, Province, City, District,
                DetailAddress, IsDefault, CreatedAt)
            VALUES (
                :AddressId, :CustomerId, :ReceiverName, :Phone, :Province, :City, :District,
                :DetailAddress, :IsDefault, SYSDATE)";

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, address, transaction);
            return address.AddressId;
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
        string customerId,
        string addressId,
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
        string customerId,
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
        string customerId,
        string addressId,
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
