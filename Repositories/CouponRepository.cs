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

    /// <summary>减少券模板剩余数量(防超发)</summary>
    public async Task<bool> DecrementCouponStockAsync(int couponId, IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var affected = await connection.ExecuteAsync(
                @"UPDATE Mkt_Coupons
                  SET RemainingQuantity = RemainingQuantity - 1
                  WHERE CouponId = :CouponId AND RemainingQuantity > 0",
                new { CouponId = couponId },
                transaction);
            return affected > 0;
        });
    }
}
