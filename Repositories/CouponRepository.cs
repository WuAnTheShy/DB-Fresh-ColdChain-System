using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 优惠券数据访问层 - Mkt_Coupons, Mkt_CouponRecords
/// </summary>
public class CouponRepository : BaseRepository
{
    public CouponRepository(IConfiguration configuration) : base(configuration) { }

    /// <summary>查询用户可用优惠券列表</summary>
    public async Task<List<MktCouponRecord>> GetUserCouponsAsync(int customerId)
    {
        using var conn = CreateConnection();
        return (await conn.QueryAsync<MktCouponRecord>(
            @"SELECT r.* FROM Mkt_CouponRecords r
              JOIN Mkt_Coupons c ON r.CouponId = c.CouponId
              WHERE r.CustomerId = :CustomerId AND r.Status = 0 AND c.Status = 1
                AND c.StartTime <= SYSDATE AND c.EndTime >= SYSDATE",
            new { CustomerId = customerId })).ToList();
    }

    /// <summary>查询优惠券模板详情</summary>
    public async Task<MktCoupon?> GetCouponTemplateAsync(int couponId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MktCoupon>(
            "SELECT * FROM Mkt_Coupons WHERE CouponId = :CouponId",
            new { CouponId = couponId });
    }

    /// <summary>标记优惠券为已使用</summary>
    public async Task UseCouponAsync(int recordId, int orderId, IDbTransaction? transaction = null)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Mkt_CouponRecords 
              SET Status = 1, OrderId = :OrderId, UsedAt = SYSDATE 
              WHERE RecordId = :RecordId",
            new { RecordId = recordId, OrderId = orderId }, transaction);
    }

    /// <summary>减少券模板剩余数量(防超发)</summary>
    public async Task<bool> DecrementCouponStockAsync(int couponId, IDbTransaction? transaction = null)
    {
        using var conn = CreateConnection();
        var affected = await conn.ExecuteAsync(
            @"UPDATE Mkt_Coupons 
              SET RemainingQuantity = RemainingQuantity - 1 
              WHERE CouponId = :CouponId AND RemainingQuantity > 0",
            new { CouponId = couponId }, transaction);
        return affected > 0;
    }
}
