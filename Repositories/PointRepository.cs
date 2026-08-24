using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 积分与会员数据访问层 - Crm_PointLogs, Crm_MemberLevels
/// </summary>
public class PointRepository : B_BaseRepository, IPointRepository
{
    public PointRepository(IConfiguration configuration) : base(configuration) { }

    /// <summary>写入积分流水（防篡改审计）</summary>
    public async Task InsertLogAsync(CrmPointLog log, IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                @"INSERT INTO Crm_PointLogs (PointLogId, CustomerId, ChangeAmount, BalanceAfter, ChangeType, OrderId, CreatedAt)
                  VALUES (:PointLogId, :CustomerId, :ChangeAmount, :BalanceAfter, :ChangeType, :OrderId, SYSDATE)",
                log,
                transaction);
        });
    }

    /// <summary>检查订单对应类型的积分流水是否已经存在。</summary>
    public async Task<bool> HasPointLogAsync(
        string customerId,
        string orderId,
        string changeType,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
        {
            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1)
                  FROM Crm_PointLogs
                  WHERE CustomerId = :CustomerId
                    AND OrderId = :OrderId
                    AND ChangeType = :ChangeType",
                new
                {
                    CustomerId = customerId,
                    OrderId = orderId,
                    ChangeType = changeType
                },
                transaction);
            return count > 0;
        });
    }

    /// <summary>获取所有会员等级（按消费门槛升序）</summary>
    public async Task<List<CrmMemberLevel>> GetAllLevelsAsync(IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmMemberLevel>(
                "SELECT * FROM Crm_MemberLevels ORDER BY MinSpent ASC",
                transaction: transaction)).ToList());
    }

    /// <summary>按ID读取会员等级，用于计算本次订单积分倍率</summary>
    public async Task<CrmMemberLevel?> GetLevelByIdAsync(
        string memberLevelId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmMemberLevel>(
                @"SELECT * FROM Crm_MemberLevels
                  WHERE MemberLevelId = :MemberLevelId",
                new { MemberLevelId = memberLevelId },
                transaction));
    }

    /// <summary>按累计消费查询当前应处的最高会员等级</summary>
    public async Task<CrmMemberLevel?> GetLevelForSpentAsync(
        decimal totalSpent,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmMemberLevel>(
                @"SELECT * FROM Crm_MemberLevels
                  WHERE MinSpent <= :TotalSpent
                  ORDER BY MinSpent DESC, MemberLevelId DESC
                  FETCH FIRST 1 ROWS ONLY",
                new { TotalSpent = totalSpent },
                transaction));
    }
}
