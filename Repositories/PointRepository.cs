using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 积分与会员数据访问层 - Crm_PointLogs, Crm_MemberLevels
/// </summary>
public class PointRepository : BaseRepository
{
    public PointRepository(IConfiguration configuration) : base(configuration) { }

    /// <summary>写入积分流水（防篡改审计）</summary>
    public async Task InsertLogAsync(CrmPointLog log, IDbTransaction? transaction = null)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await connection.ExecuteAsync(
                @"INSERT INTO Crm_PointLogs (CustomerId, ChangeAmount, BalanceAfter, ChangeType, OrderId, CreatedAt)
                  VALUES (:CustomerId, :ChangeAmount, :BalanceAfter, :ChangeType, :OrderId, SYSDATE)",
                log,
                transaction);
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
}
