using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 订单数据访问层 - 只负责 Biz_Orders 和 Biz_OrderDetails 的 CRUD
public class OrderRepository : B_BaseRepository, IOrderRepository
{
    // 售后聚合时按订单号 IN 过滤的上限（防 IN 列表过长）
    private const int MaxRefundOrderIdFilterCount = 500;

    public OrderRepository(IConfiguration configuration) : base(configuration) { }

    // Biz_Orders

    // 创建订单
    public async Task<string> CreateOrderAsync(BizOrder order, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO Biz_Orders (
                OrderId, OrderNo, CustomerId, CheckoutBatchId, PromoterId, AddressId, ReceiverName, ReceiverPhone,
                ReceiverProvince, ReceiverCity, ReceiverDistrict, ReceiverDetailAddress,
                TotalAmount, DiscountAmount, FreightAmount, FreightQuoteSnapshot,
                FinalAmount, PointsEarned, PointsUsed, PointsDiscountAmount, OrderStatus, PaymentExpiresAt, CreatedAt)
            VALUES (
                :OrderId, :OrderNo, :CustomerId, :CheckoutBatchId, :PromoterId, :AddressId, :ReceiverName, :ReceiverPhone,
                :ReceiverProvince, :ReceiverCity, :ReceiverDistrict, :ReceiverDetailAddress,
                :TotalAmount, :DiscountAmount, :FreightAmount, :FreightQuoteSnapshot,
                :FinalAmount, :PointsEarned, :PointsUsed, :PointsDiscountAmount, :OrderStatus, :PaymentExpiresAt, SYSDATE)";

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, order, transaction);
            return order.OrderId;
        });
    }

    // 根据ID查订单
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
                   {CreateOrderFilterSql(request)}",
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
                          MIN(d.Quantity) KEEP (DENSE_RANK FIRST ORDER BY d.OrderDetailId) AS FirstProductQuantity,
                          o.CreatedAt
                          ,o.PaymentExpiresAt
                   FROM Biz_Orders o
                   JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                   LEFT JOIN Crm_Promoters p ON p.PromoterId = o.PromoterId
                   LEFT JOIN Biz_OrderDetails d ON d.OrderId = o.OrderId
                   {CreateOrderFilterSql(request)}
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

    public async Task<List<OrderCardProductItem>> GetOrderCardItemsAsync(
        IReadOnlyCollection<string> orderIds,
        IDbTransaction? transaction = null)
    {
        if (orderIds.Count == 0)
            return [];

        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<OrderCardProductItem>(
                @"SELECT d.OrderId,
                         d.OrderDetailId,
                         d.ProductId,
                         d.ProductName,
                         d.SupplierId,
                         d.Quantity,
                         d.UnitPrice,
                         d.SubTotal,
                         MIN(productImage.ImageUrl) KEEP (
                             DENSE_RANK FIRST ORDER BY
                                 CASE WHEN productImage.SupplierId = d.SupplierId THEN 0 ELSE 1 END,
                                 productImage.SortOrder NULLS LAST,
                                 productImage.CreateTime NULLS LAST,
                                 productImage.ImageId NULLS LAST
                         ) AS ImageUrl
                  FROM Biz_OrderDetails d
                  LEFT JOIN Inv_ProductImages productImage
                    ON productImage.ProductId = d.ProductId
                   AND (productImage.SupplierId = d.SupplierId OR productImage.SupplierId IS NULL)
                  WHERE d.OrderId IN :OrderIds
                  GROUP BY d.OrderId,
                           d.OrderDetailId,
                           d.ProductId,
                           d.ProductName,
                           d.SupplierId,
                           d.Quantity,
                           d.UnitPrice,
                           d.SubTotal
                  ORDER BY d.OrderId, d.OrderDetailId",
                new { OrderIds = orderIds },
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
                          o.ReceiverProvince,
                          o.ReceiverCity,
                          o.ReceiverDistrict,
                          o.ReceiverDetailAddress,
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
                            o.ReceiverProvince,
                            o.ReceiverCity,
                            o.ReceiverDistrict,
                            o.ReceiverDetailAddress,
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
                         p.PromoterName,
                         o.AddressId,
                         c.CustomerName,
                         o.ReceiverName,
                         o.ReceiverPhone,
                         o.ReceiverProvince,
                         o.ReceiverCity,
                         o.ReceiverDistrict,
                         o.ReceiverDetailAddress,
                         o.TotalAmount,
                         o.DiscountAmount,
                         o.FreightAmount,
                         o.FreightQuoteSnapshot,
                         o.FinalAmount,
                         o.PointsEarned,
                         o.PointsUsed,
                         o.PointsDiscountAmount,
                         o.OrderStatus,
                         o.StatusBeforeRefund,
                         o.PaymentExpiresAt,
                         o.CreatedAt,
                         o.UpdatedAt
                  FROM Biz_Orders o
                  JOIN Crm_Customers c ON c.CustomerId = o.CustomerId
                  LEFT JOIN Crm_Promoters p ON p.PromoterId = o.PromoterId
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

    // 带旧状态条件的原子状态更新
    public async Task<bool> TryUpdateStatusAsync(
        string orderId,
        OrderStatus expectedStatus,
        OrderStatus targetStatus,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                // 状态一旦变更即离开“退款审核中”，无需再保留回退记录
                @"UPDATE Biz_Orders
                  SET OrderStatus = :TargetStatus,
                      StatusBeforeRefund = NULL,
                      UpdatedAt = SYSDATE
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

    // 进入“退款审核中”：订单状态改为 REFUND_REVIEWING，
    // 并在首次进入时把原状态记入 StatusBeforeRefund 供审核驳回/取消申请时回退（重复进入幂等）。
    // 未支付/已取消/已退款的订单不参与退款审核，返回 false 且不改动订单。
    public async Task<bool> TryEnterRefundReviewAsync(
        string orderId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Biz_Orders
                  SET StatusBeforeRefund = NVL(StatusBeforeRefund, OrderStatus),
                      OrderStatus = :RefundReviewing,
                      UpdatedAt = SYSDATE
                  WHERE OrderId = :OrderId
                    AND OrderStatus IN (:Paid, :Shipped, :Completed, :Refunding, :RefundReviewing)",
                new
                {
                    OrderId = orderId,
                    Paid = OrderStatusCodes.Paid,
                    Shipped = OrderStatusCodes.Shipped,
                    Completed = OrderStatusCodes.Completed,
                    Refunding = OrderStatusCodes.Refunding,
                    RefundReviewing = OrderStatusCodes.RefundReviewing
                },
                transaction);
            return affected == 1;
        });
    }

    // 退出“退款审核中”：把订单状态回退到申请前记录的 StatusBeforeRefund 并清空该列。
    // 订单不在退款审核中（例如已审核通过转为退款中/已退款）时不做任何改动，返回 false。
    public async Task<bool> TryRestoreStatusBeforeRefundAsync(
        string orderId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Biz_Orders
                  SET OrderStatus = StatusBeforeRefund,
                      StatusBeforeRefund = NULL,
                      UpdatedAt = SYSDATE
                  WHERE OrderId = :OrderId
                    AND OrderStatus = :RefundReviewing
                    AND StatusBeforeRefund IS NOT NULL",
                new
                {
                    OrderId = orderId,
                    RefundReviewing = OrderStatusCodes.RefundReviewing
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

    // Biz_OrderDetails

    // 批量插入订单明细
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

    private static string CreateOrderFilterSql(OrderQueryRequest request)
    {
        // 过滤条件按需拼接：状态集合（可多个，如“退款审核中+退款中”）与退款订单号集合之间取并集；
        // 「退款售后」列表用订单号集合兜底改造前提交、订单状态未进入审核中的历史申请单。
        // 两者都为空时退化为仅按消费者/关键词过滤（管理员订单查询等场景）。
        var statusFilter = NormalizeStatuses(request).Count > 0
            ? "o.OrderStatus IN :OrderStatuses"
            : null;
        var orderIdFilter = NormalizeOrderIds(request.OrderIds).Count > 0
            ? "o.OrderId IN :RefundOrderIds"
            : null;
        var statusClause = (statusFilter, orderIdFilter) switch
        {
            (null, null) => "1 = 1",
            (not null, null) => statusFilter,
            (null, not null) => orderIdFilter,
            _ => $"({statusFilter} OR {orderIdFilter})"
        };

        return $@"WHERE (:CustomerId IS NULL OR o.CustomerId = :CustomerId)
                    AND {statusClause}
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
            // 条件未拼进 SQL 时 Dapper 按名绑定会自动忽略这两个集合参数
            OrderStatuses = NormalizeStatuses(request)
                .Select(OrderStatusCodes.ToCode)
                .ToList(),
            request.Keyword,
            Offset = offset,
            request.PageSize,
            RefundOrderIds = NormalizeOrderIds(request.OrderIds)
        };
    }

    // 状态过滤集合：服务端内部指定的 <see cref="OrderQueryRequest.OrderStatuses"/> 优先，
    // 其次退化为外部传入的单个 <see cref="OrderQueryRequest.Status"/>。
    private static IReadOnlyList<OrderStatus> NormalizeStatuses(OrderQueryRequest request)
    {
        if (request.OrderStatuses is { Count: > 0 })
        {
            return request.OrderStatuses.Distinct().ToList();
        }

        return request.Status.HasValue ? [request.Status.Value] : [];
    }

    private static IReadOnlyList<string> NormalizeOrderIds(IReadOnlyCollection<string>? orderIds)
    {
        if (orderIds == null || orderIds.Count == 0)
        {
            return [];
        }

        return orderIds
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Select(orderId => orderId.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxRefundOrderIdFilterCount)
            .ToList();
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
