using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 优惠券数据访问层 - Mkt_Coupons, Mkt_CouponRecords
/// </summary>
public class CouponRepository : BaseRepository, ICouponRepository
{
    public CouponRepository(IConfiguration configuration) : base(configuration) { }

    /// <summary>查询用户可用优惠券列表</summary>
    public async Task<List<MktCouponRecord>> GetUserCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<MktCouponRecord>(
                @"SELECT r.* FROM Mkt_CouponRecords r
                  JOIN Mkt_Coupons c ON r.CouponId = c.CouponId
                  WHERE r.CustomerId = :CustomerId AND r.Status = 0 AND c.Status = 1
                    AND c.StartTime <= SYSDATE AND c.EndTime >= SYSDATE",
                new { CustomerId = customerId },
                transaction)).ToList());
    }

    /// <summary>查询优惠券模板详情</summary>
    public async Task<MktCoupon?> GetCouponTemplateAsync(
        int couponId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<MktCoupon>(
                "SELECT * FROM Mkt_Coupons WHERE CouponId = :CouponId",
                new { CouponId = couponId },
                transaction));
    }

    /// <summary>锁定券模板，保证领券检查期间库存和状态不漂移</summary>
    public async Task<MktCoupon?> GetCouponTemplateForUpdateAsync(
        int couponId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<MktCoupon>(
                @"SELECT * FROM Mkt_Coupons
                  WHERE CouponId = :CouponId
                  FOR UPDATE",
                new { CouponId = couponId },
                transaction));
    }

    /// <summary>查询当前有效、尚有库存的券模板，并标记消费者是否已领取</summary>
    public async Task<List<ClaimableCouponItem>> GetClaimableCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<ClaimableCouponItem>(
                @"SELECT c.CouponId,
                         c.CouponName,
                         c.MinOrderAmount,
                         c.DiscountAmount,
                         c.RemainingQuantity,
                         c.EndTime,
                         CASE WHEN EXISTS (
                             SELECT 1 FROM Mkt_CouponRecords r
                             WHERE r.CouponId = c.CouponId
                               AND r.CustomerId = :CustomerId
                         ) THEN 1 ELSE 0 END AS HasClaimed
                  FROM Mkt_Coupons c
                  WHERE c.Status = 1
                    AND c.StartTime <= SYSDATE
                    AND c.EndTime >= SYSDATE
                  ORDER BY c.EndTime ASC, c.CouponId ASC",
                new { CustomerId = customerId },
                transaction)).ToList());
    }

    /// <summary>查询消费者已领取、未使用且仍在有效期内的优惠券</summary>
    public async Task<List<AvailableCouponItem>> GetAvailableCouponsAsync(
        int customerId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<AvailableCouponItem>(
                @"SELECT r.RecordId,
                         r.CouponId,
                         c.CouponName,
                         c.MinOrderAmount,
                         c.DiscountAmount,
                         c.EndTime
                  FROM Mkt_CouponRecords r
                  JOIN Mkt_Coupons c ON c.CouponId = r.CouponId
                  WHERE r.CustomerId = :CustomerId
                    AND r.Status = 0
                    AND c.Status = 1
                    AND c.StartTime <= SYSDATE
                    AND c.EndTime >= SYSDATE
                  ORDER BY c.EndTime ASC, r.RecordId ASC",
                new { CustomerId = customerId },
                transaction)).ToList());
    }

    public async Task<bool> HasCustomerClaimedCouponAsync(
        int customerId,
        int couponId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM Mkt_CouponRecords
                  WHERE CustomerId = :CustomerId AND CouponId = :CouponId",
                new { CustomerId = customerId, CouponId = couponId },
                transaction);
            return count > 0;
        });
    }

    public async Task<int> CreateCouponRecordAsync(
        int customerId,
        int couponId,
        IDbTransaction transaction)
    {
        const string sql = @"
            INSERT INTO Mkt_CouponRecords (
                CouponId, CustomerId, Status, CreatedAt)
            VALUES (:CouponId, :CustomerId, 0, SYSDATE)
            RETURNING RecordId INTO :RecordId";

        var parameters = new DynamicParameters(new
        {
            CustomerId = customerId,
            CouponId = couponId
        });
        parameters.Add(
            "RecordId",
            dbType: DbType.Int32,
            direction: ParameterDirection.Output);

        return await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(sql, parameters, transaction);
            return parameters.Get<int>("RecordId");
        });
    }

    /// <summary>锁定并读取本次订单可用的用户券</summary>
    public async Task<MktCouponUsage?> GetUsableCouponForUpdateAsync(
        int recordId,
        int customerId,
        decimal orderAmount,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<MktCouponUsage>(
                @"SELECT r.RecordId, r.CouponId, c.CouponName, c.DiscountAmount
                  FROM Mkt_CouponRecords r
                  JOIN Mkt_Coupons c ON r.CouponId = c.CouponId
                  WHERE r.RecordId = :RecordId
                    AND r.CustomerId = :CustomerId
                    AND r.Status = 0
                    AND c.Status = 1
                    AND c.StartTime <= SYSDATE
                    AND c.EndTime >= SYSDATE
                    AND c.MinOrderAmount <= :OrderAmount
                  FOR UPDATE OF r.Status, c.Status",
                new
                {
                    RecordId = recordId,
                    CustomerId = customerId,
                    OrderAmount = orderAmount
                },
                transaction));
    }

    /// <summary>以条件更新方式原子核销优惠券</summary>
    public async Task<bool> TryUseCouponAsync(
        int recordId,
        int customerId,
        int orderId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Mkt_CouponRecords
                  SET Status = 1, OrderId = :OrderId, UsedAt = SYSDATE
                  WHERE RecordId = :RecordId
                    AND CustomerId = :CustomerId
                    AND Status = 0",
                new
                {
                    RecordId = recordId,
                    CustomerId = customerId,
                    OrderId = orderId
                },
                transaction);
            return affected == 1;
        });
    }

    /// <summary>取消订单时归还该订单核销的用户券</summary>
    public async Task<int> RestoreCouponForCancelledOrderAsync(
        int orderId,
        int customerId,
        IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.ExecuteAsync(
                @"UPDATE Mkt_CouponRecords
                  SET Status = 0, OrderId = NULL, UsedAt = NULL
                  WHERE OrderId = :OrderId
                    AND CustomerId = :CustomerId
                    AND Status = 1",
                new { OrderId = orderId, CustomerId = customerId },
                transaction));
    }

    /// <summary>减少券模板剩余数量(防超发)</summary>
    public async Task<bool> DecrementCouponStockAsync(int couponId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Mkt_Coupons
                  SET RemainingQuantity = RemainingQuantity - 1
                  WHERE CouponId = :CouponId
                    AND Status = 1
                    AND StartTime <= SYSDATE
                    AND EndTime >= SYSDATE
                    AND RemainingQuantity > 0",
                new { CouponId = couponId },
                transaction);
            return affected == 1;
        });
    }
}
