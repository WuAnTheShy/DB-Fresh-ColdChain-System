using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 订单数据访问层 - 只负责 Biz_Orders 和 Biz_OrderDetails 的 CRUD
/// </summary>
public class OrderRepository : BaseRepository, IOrderRepository
{
    public OrderRepository(IConfiguration configuration) : base(configuration) { }

    // ========== Biz_Orders ==========

    /// <summary>创建订单</summary>
    public async Task<int> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO Biz_Orders (
                OrderNo, CustomerId, PromoterId, AddressId, ReceiverName, ReceiverPhone,
                ShippingAddress, TotalAmount, DiscountAmount, FreightAmount,
                FinalAmount, PointsEarned, OrderStatus, CreatedAt)
            VALUES (
                :OrderNo, :CustomerId, :PromoterId, :AddressId, :ReceiverName, :ReceiverPhone,
                :ShippingAddress, :TotalAmount, :DiscountAmount, :FreightAmount,
                :FinalAmount, :PointsEarned, :OrderStatus, SYSDATE)
            RETURNING OrderId INTO :OrderId";

        var parameters = new DynamicParameters(order);
        parameters.Add("OrderId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, parameters, transaction);
            return parameters.Get<int>("OrderId");
        });
    }

    /// <summary>根据ID查订单</summary>
    public async Task<BizOrder?> GetByIdAsync(int orderId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<BizOrder>(
                "SELECT * FROM Biz_Orders WHERE OrderId = :OrderId",
                new { OrderId = orderId },
                transaction));
    }

    public async Task<BizOrder?> GetByIdForUpdateAsync(
        int orderId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders
                  WHERE OrderId = :OrderId
                  FOR UPDATE",
                new { OrderId = orderId },
                transaction));
    }

    public async Task<List<BizOrder>> GetOrdersForCommissionExpiryAsync(
        DateTime threshold,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders
                                    WHERE OrderStatus IN (1, 2)
                                        AND NVL(CommSettlementDate, CreatedAt) <= :Threshold",
                new { Threshold = threshold },
                transaction)).ToList());
    }

    public async Task<int> CountOrdersAsync(
        OrderQueryRequest request,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(1)
                   FROM Biz_Orders o
                   JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                   {CreateOrderFilterSql()}",
                CreateOrderQueryParameters(request),
                transaction));
    }

    public async Task<List<OrderListItem>> GetOrdersAsync(
        OrderQueryRequest request,
        int offset,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<OrderListItem>(
                $@"SELECT o.OrderId,
                          o.OrderNo,
                          o.CustomerId,
                          c.CustomerName,
                          o.FinalAmount,
                          o.OrderStatus,
                          COUNT(d.OrderDetailId) AS ItemCount,
                          COUNT(DISTINCT d.SupplierId) AS SupplierCount,
                          o.CreatedAt
                   FROM Biz_Orders o
                   JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                   LEFT JOIN Biz_OrderDetails d ON d.OrderId = o.OrderId
                   {CreateOrderFilterSql()}
                   GROUP BY o.OrderId,
                            o.OrderNo,
                            o.CustomerId,
                            c.CustomerName,
                            o.FinalAmount,
                            o.OrderStatus,
                            o.CreatedAt
                   ORDER BY o.CreatedAt DESC, o.OrderId DESC
                   OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY",
                CreateOrderQueryParameters(request, offset),
                transaction)).ToList());
    }

    public async Task<OrderDetailHeader?> GetDetailHeaderAsync(
        int orderId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<OrderDetailHeader>(
                @"SELECT o.OrderId,
                         o.OrderNo,
                         o.CustomerId,
                         o.AddressId,
                         c.CustomerName,
                         o.ReceiverName,
                         o.ReceiverPhone,
                         o.ShippingAddress,
                         o.TotalAmount,
                         o.DiscountAmount,
                         o.FreightAmount,
                         o.FinalAmount,
                         o.PointsEarned,
                         o.OrderStatus,
                         o.CreatedAt,
                         o.UpdatedAt
                  FROM Biz_Orders o
                  JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                  WHERE o.OrderId = :OrderId",
                new { OrderId = orderId },
                transaction));
    }

    public async Task<List<BizOrderDetail>> GetDetailsAsync(
        int orderId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrderDetail>(
                @"SELECT * FROM Biz_OrderDetails
                  WHERE OrderId = :OrderId
                  ORDER BY SupplierId ASC, OrderDetailId ASC",
                new { OrderId = orderId },
                transaction)).ToList());
    }

    /// <summary>带旧状态条件的原子状态更新</summary>
    public async Task<bool> TryUpdateStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Biz_Orders
                  SET OrderStatus = :TargetStatus, UpdatedAt = SYSDATE
                  WHERE OrderId = :OrderId AND OrderStatus = :ExpectedStatus",
                new
                {
                    OrderId = orderId,
                    ExpectedStatus = (int)expectedStatus,
                    TargetStatus = (int)targetStatus
                },
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> TryUpdateCommissionSettlementAsync(
        int orderId,
        decimal? commBaseAmount,
        decimal? commBonusAmount,
        DateTime? commSettlementDate,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Biz_Orders
                  SET CommBaseAmount = :CommBaseAmount,
                      CommBonusAmount = :CommBonusAmount,
                      CommSettlementDate = :CommSettlementDate,
                      UpdatedAt = SYSDATE
                  WHERE OrderId = :OrderId",
                new
                {
                    OrderId = orderId,
                    CommBaseAmount = commBaseAmount,
                    CommBonusAmount = commBonusAmount,
                    CommSettlementDate = commSettlementDate
                },
                transaction);
            return affected == 1;
        });
    }

    // ========== Biz_OrderDetails ==========

    /// <summary>批量插入订单明细</summary>
    public async Task InsertDetailsAsync(IEnumerable<BizOrderDetail> details, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO Biz_OrderDetails (OrderId, ProductId, ProductName, Quantity, UnitPrice, SubTotal, SupplierId)
            VALUES (:OrderId, :ProductId, :ProductName, :Quantity, :UnitPrice, :SubTotal, :SupplierId)";
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, details, transaction);
        });
    }

    private static string CreateOrderFilterSql()
    {
           return @"WHERE (:CustomerId IS NULL OR o.CustomerId = :CustomerId)
                    AND (:OrderStatus IS NULL OR o.OrderStatus = :OrderStatus)
                    AND (:Keyword IS NULL
                        OR o.OrderNo LIKE '%' || :Keyword || '%'
                        OR c.CustomerName LIKE '%' || :Keyword || '%')";
    }

    private static object CreateOrderQueryParameters(
        OrderQueryRequest request,
        int offset = 0)
    {
        return new
        {
            request.CustomerId,
            OrderStatus = request.Status.HasValue
                ? (int?)request.Status.Value
                : null,
            request.Keyword,
            Offset = offset,
            request.PageSize
        };
    }
}
