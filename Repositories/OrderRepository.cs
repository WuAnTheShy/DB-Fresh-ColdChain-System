using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 订单数据访问层 - 只负责 Biz_Orders 和 Biz_OrderDetails 的 CRUD
/// </summary>
public class OrderRepository : B_BaseRepository, IOrderRepository
{
    public OrderRepository(IConfiguration configuration) : base(configuration) { }

    // ========== Biz_Orders ==========

    /// <summary>创建订单</summary>
    public async Task<string> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO Biz_Orders (
                OrderId, OrderNo, CustomerId, CheckoutBatchId, PromoterId, AddressId, ReceiverName, ReceiverPhone,
                ShippingAddress, TotalAmount, DiscountAmount, FreightAmount, FreightQuoteSnapshot,
                FinalAmount, PointsEarned, PointsUsed, PointsDiscountAmount, OrderStatus, PaymentExpiresAt, CreatedAt)
            VALUES (
                :OrderId, :OrderNo, :CustomerId, :CheckoutBatchId, :PromoterId, :AddressId, :ReceiverName, :ReceiverPhone,
                :ShippingAddress, :TotalAmount, :DiscountAmount, :FreightAmount, :FreightQuoteSnapshot,
                :FinalAmount, :PointsEarned, :PointsUsed, :PointsDiscountAmount, :OrderStatus, :PaymentExpiresAt, SYSDATE)";

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, order, transaction);
            return order.OrderId;
        });
    }

    /// <summary>根据ID查订单</summary>
    public async Task<BizOrder?> GetByIdAsync(string orderId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<BizOrder>(
                "SELECT * FROM Biz_Orders WHERE OrderId = :OrderId",
                new { OrderId = orderId },
                transaction));
    }

    public async Task<BizOrder?> GetByIdForUpdateAsync(
        string orderId,
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

    public async Task<List<BizOrder>> GetByCheckoutBatchAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders
                  WHERE CheckoutBatchId = :CheckoutBatchId
                    AND CustomerId = :CustomerId
                  ORDER BY OrderId",
                new { CheckoutBatchId = checkoutBatchId, CustomerId = customerId },
                transaction)).ToList());
    }

    public async Task<List<BizOrder>> GetByCheckoutBatchForUpdateAsync(
        string checkoutBatchId,
        string customerId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders
                  WHERE CheckoutBatchId = :CheckoutBatchId
                    AND CustomerId = :CustomerId
                  ORDER BY OrderId
                  FOR UPDATE",
                new { CheckoutBatchId = checkoutBatchId, CustomerId = customerId },
                transaction)).ToList());
    }

    public async Task<List<BizOrder>> GetByCheckoutBatchForUpdateAsync(
        string checkoutBatchId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders WHERE CheckoutBatchId = :CheckoutBatchId
                  ORDER BY OrderId FOR UPDATE",
                new { CheckoutBatchId = checkoutBatchId }, transaction)).ToList());
    }

    public async Task<List<string>> GetExpiredPendingCheckoutBatchIdsAsync(
        DateTime now,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<string>(
                @"SELECT DISTINCT CheckoutBatchId FROM Biz_Orders
                  WHERE CheckoutBatchId IS NOT NULL AND OrderStatus = 'PENDING_PAYMENT'
                    AND PaymentExpiresAt <= :Now",
                new { Now = now }, transaction)).ToList());
    }

    public async Task<List<BizOrder>> GetShippedOrdersBeforeAsync(DateTime threshold, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders WHERE OrderStatus = 'SHIPPED'
                    AND NVL(UpdatedAt, CreatedAt) <= :Threshold",
                new { Threshold = threshold }, transaction)).ToList());
    }

    public async Task<List<BizOrder>> GetOrdersForCommissionExpiryAsync(
        DateTime threshold,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<BizOrder>(
                @"SELECT * FROM Biz_Orders
                                    WHERE OrderStatus IN ('PAID', 'SHIPPED')
                                        AND NVL(CommSettlementDate, CreatedAt) <= :Threshold",
                new { Threshold = threshold },
                transaction)).ToList());
    }

    public async Task<decimal> GetCompletedSpentBeforeAsync(string customerId, DateTime cutoff, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            await connection.ExecuteScalarAsync<decimal>(
                @"SELECT NVL(SUM(FinalAmount), 0) FROM Biz_Orders
                  WHERE CustomerId = :CustomerId
                    AND OrderStatus = 'COMPLETED'
                    AND NVL(UpdatedAt, CreatedAt) < :Cutoff",
                new { CustomerId = customerId, Cutoff = cutoff }, transaction));
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
                          o.CheckoutBatchId,
                          o.PromoterId,
                          p.PromoterName,
                          c.CustomerName,
                          o.FinalAmount,
                          o.OrderStatus,
                          COUNT(DISTINCT d.OrderDetailId) AS ItemCount,
                          COUNT(DISTINCT d.SupplierId) AS SupplierCount,
                          MIN(d.ProductId) KEEP (DENSE_RANK FIRST ORDER BY d.OrderDetailId) AS FirstProductId,
                          MIN(d.ProductName) KEEP (DENSE_RANK FIRST ORDER BY d.OrderDetailId) AS FirstProductName,
                          MIN(productImage.ImageUrl) KEEP (
                              DENSE_RANK FIRST ORDER BY
                                  d.OrderDetailId,
                                  CASE WHEN productImage.SupplierId = d.SupplierId THEN 0 ELSE 1 END,
                                  productImage.SortOrder NULLS LAST,
                                  productImage.CreateTime NULLS LAST,
                                  productImage.ImageId NULLS LAST
                          ) AS FirstProductImageUrl,
                          MIN(d.Quantity) KEEP (DENSE_RANK FIRST ORDER BY d.OrderDetailId) AS FirstProductQuantity,
                          o.CreatedAt
                          ,o.PaymentExpiresAt
                   FROM Biz_Orders o
                   JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                   LEFT JOIN Crm_Promoters p ON p.PromoterId = o.PromoterId
                   LEFT JOIN Biz_OrderDetails d ON d.OrderId = o.OrderId
                   LEFT JOIN Inv_ProductImages productImage
                     ON productImage.ProductId = d.ProductId
                    AND (productImage.SupplierId = d.SupplierId OR productImage.SupplierId IS NULL)
                   {CreateOrderFilterSql()}
                   GROUP BY o.OrderId,
                            o.OrderNo,
                            o.CustomerId,
                            o.CheckoutBatchId,
                            o.PromoterId,
                            p.PromoterName,
                            c.CustomerName,
                            o.FinalAmount,
                            o.OrderStatus,
                            o.CreatedAt,
                            o.PaymentExpiresAt
                   ORDER BY o.CreatedAt DESC, o.OrderId DESC
                   OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY",
                CreateOrderQueryParameters(request, offset),
                transaction)).ToList());
    }

    public async Task<int> CountSupplierFulfillmentOrdersAsync(
        string supplierId,
        SupplierFulfillmentQuery query,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(DISTINCT o.OrderId)
                   FROM Biz_Orders o
                   JOIN Biz_OrderDetails d ON d.OrderId = o.OrderId
                   WHERE d.SupplierId = :SupplierId
                     AND o.OrderStatus IN ('PAID', 'SHIPPED', 'COMPLETED')
                     AND (:OrderStatus IS NULL OR o.OrderStatus = :OrderStatus)
                     AND (:Keyword IS NULL
                         OR o.OrderNo LIKE '%' || :Keyword || '%'
                         OR o.ReceiverName LIKE '%' || :Keyword || '%'
                         OR o.ReceiverPhone LIKE '%' || :Keyword || '%')",
                CreateSupplierFulfillmentParameters(supplierId, query),
                transaction));
    }

    public async Task<List<SupplierFulfillmentOrderListItem>> GetSupplierFulfillmentOrdersAsync(
        string supplierId,
        SupplierFulfillmentQuery query,
        int offset,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<SupplierFulfillmentOrderListItem>(
                $@"SELECT o.OrderId,
                          o.OrderNo,
                          c.CustomerName,
                          o.ReceiverName,
                          o.ReceiverPhone,
                          o.ShippingAddress,
                          o.OrderStatus,
                          COUNT(d.OrderDetailId) AS ItemCount,
                          SUM(d.Quantity) AS TotalQuantity,
                          SUM(d.SubTotal) AS SupplierAmount,
                          o.CreatedAt
                   FROM Biz_Orders o
                   JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                   JOIN Biz_OrderDetails d ON d.OrderId = o.OrderId
                   WHERE d.SupplierId = :SupplierId
                     AND o.OrderStatus IN ('PAID', 'SHIPPED', 'COMPLETED')
                     AND (:OrderStatus IS NULL OR o.OrderStatus = :OrderStatus)
                     AND (:Keyword IS NULL
                         OR o.OrderNo LIKE '%' || :Keyword || '%'
                         OR o.ReceiverName LIKE '%' || :Keyword || '%'
                         OR o.ReceiverPhone LIKE '%' || :Keyword || '%')
                   GROUP BY o.OrderId,
                            o.OrderNo,
                            c.CustomerName,
                            o.ReceiverName,
                            o.ReceiverPhone,
                            o.ShippingAddress,
                            o.OrderStatus,
                            o.CreatedAt
                   ORDER BY o.CreatedAt DESC, o.OrderId DESC
                   OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY",
                CreateSupplierFulfillmentParameters(supplierId, query, offset),
                transaction)).ToList());
    }

    public async Task<OrderDetailHeader?> GetDetailHeaderAsync(
        string orderId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<OrderDetailHeader>(
                @"SELECT o.OrderId,
                         o.OrderNo,
                         o.CustomerId,
                         o.CheckoutBatchId,
                         o.PromoterId,
                         o.AddressId,
                         c.CustomerName,
                         o.ReceiverName,
                         o.ReceiverPhone,
                         o.ShippingAddress,
                         o.TotalAmount,
                         o.DiscountAmount,
                         o.FreightAmount,
                         o.FreightQuoteSnapshot,
                         o.FinalAmount,
                         o.PointsEarned,
                         o.PointsUsed,
                         o.PointsDiscountAmount,
                         o.OrderStatus,
                         o.PaymentExpiresAt,
                         o.CreatedAt,
                         o.UpdatedAt
                  FROM Biz_Orders o
                  JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                  WHERE o.OrderId = :OrderId",
                new { OrderId = orderId },
                transaction));
    }

    public async Task<List<BizOrderDetail>> GetDetailsAsync(
        string orderId,
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
        string orderId,
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
                    ExpectedStatus = OrderStatusCodes.ToCode(expectedStatus),
                    TargetStatus = OrderStatusCodes.ToCode(targetStatus)
                },
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> TryUpdateCommissionSettlementAsync(
        string orderId,
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
            INSERT INTO Biz_OrderDetails (
                OrderDetailId, OrderId, ProductId, ProductName,
                Quantity, UnitPrice, SubTotal, SupplierId, ReceiptStatus)
            VALUES (
                :OrderDetailId, :OrderId, :ProductId, :ProductName,
                :Quantity, :UnitPrice, :SubTotal, :SupplierId, :ReceiptStatus)";
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, details, transaction);
        });
    }

    public async Task<bool> TryConfirmDetailReceiptAsync(
        string orderDetailId,
        string orderId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Biz_OrderDetails
                  SET ReceiptStatus = 'RECEIVED', ReceivedAt = :ReceivedAt
                  WHERE OrderDetailId = :OrderDetailId
                    AND OrderId = :OrderId
                    AND NVL(ReceiptStatus, 'PENDING') = 'PENDING'",
                new { OrderDetailId = orderDetailId, OrderId = orderId, ReceivedAt = DateTime.Now },
                transaction);
            return affected == 1;
        });
    }

    public async Task<bool> HasUnreceivedDetailsExceptAsync(
        string orderId,
        string excludedOrderDetailId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
            await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM Biz_OrderDetails
                  WHERE OrderId = :OrderId
                    AND OrderDetailId <> :ExcludedOrderDetailId
                    AND NVL(ReceiptStatus, 'PENDING') = 'PENDING'",
                new { OrderId = orderId, ExcludedOrderDetailId = excludedOrderDetailId },
                transaction) > 0);
    }

    public async Task<bool> UpdatePointsEarnedAsync(
        string orderId,
        int pointsEarned,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
            await connection.ExecuteAsync(
                @"UPDATE Biz_Orders SET PointsEarned = :PointsEarned, UpdatedAt = SYSDATE
                  WHERE OrderId = :OrderId",
                new { OrderId = orderId, PointsEarned = pointsEarned },
                transaction) == 1);
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
                ? OrderStatusCodes.ToCode(request.Status.Value)
                : null,
            request.Keyword,
            Offset = offset,
            request.PageSize
        };
    }

    private static object CreateSupplierFulfillmentParameters(
        string supplierId,
        SupplierFulfillmentQuery query,
        int offset = 0)
    {
        return new
        {
            SupplierId = supplierId,
            OrderStatus = query.Status.HasValue
                ? OrderStatusCodes.ToCode(query.Status.Value)
                : null,
            Keyword = string.IsNullOrWhiteSpace(query.Keyword)
                ? null
                : query.Keyword.Trim(),
            Offset = offset,
            query.PageSize
        };
    }
}
