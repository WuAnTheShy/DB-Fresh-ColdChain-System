using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 订单数据访问层 - 只负责 Biz_Orders 和 Biz_OrderDetails 的 CRUD
/// </summary>
public class OrderRepository : BaseRepository
{
    public OrderRepository(IConfiguration configuration) : base(configuration) { }

    // ========== Biz_Orders ==========

    /// <summary>创建订单</summary>
    public async Task<int> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null)
    {
        using var conn = CreateConnection();
        if (transaction != null) conn.Open(); // 如果外部传入了事务，参与该事务

        var sql = @"
            INSERT INTO Biz_Orders (OrderNo, CustomerId, AddressId, TotalAmount, 
                DiscountAmount, FreightAmount, FinalAmount, PointsEarned, OrderStatus, CreatedAt)
            VALUES (:OrderNo, :CustomerId, :AddressId, :TotalAmount, 
                :DiscountAmount, :FreightAmount, :FinalAmount, :PointsEarned, :OrderStatus, SYSDATE)
            RETURNING OrderId INTO :OrderId";

        var parameters = new DynamicParameters(order);
        parameters.Add("OrderId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

        await conn.ExecuteAsync(sql, parameters, transaction);
        return parameters.Get<int>("OrderId");
    }

    /// <summary>根据ID查订单</summary>
    public async Task<BizOrder?> GetByIdAsync(int orderId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<BizOrder>(
            "SELECT * FROM Biz_Orders WHERE OrderId = :OrderId", new { OrderId = orderId });
    }

    /// <summary>更新订单状态</summary>
    public async Task UpdateStatusAsync(int orderId, int status, IDbTransaction? transaction = null)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Biz_Orders SET OrderStatus = :Status, UpdatedAt = SYSDATE WHERE OrderId = :OrderId",
            new { OrderId = orderId, Status = status }, transaction);
    }

    // ========== Biz_OrderDetails ==========

    /// <summary>批量插入订单明细</summary>
    public async Task InsertDetailsAsync(IEnumerable<BizOrderDetail> details, IDbTransaction? transaction = null)
    {
        using var conn = CreateConnection();
        var sql = @"
            INSERT INTO Biz_OrderDetails (OrderId, ProductId, ProductName, Quantity, UnitPrice, SubTotal, SupplierId)
            VALUES (:OrderId, :ProductId, :ProductName, :Quantity, :UnitPrice, :SubTotal, :SupplierId)";
        await conn.ExecuteAsync(sql, details, transaction);
    }
}
