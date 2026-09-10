using System.Data;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 积分与会员数据访问层 - Crm_PointLogs, Crm_MemberLevels
public class PointRepository : B_BaseRepository, IPointRepository
{
    private const string ConsumerLevelFilter =
        "('00000000000000000000000000000001','MEMBER_LEVEL_1','MEMBER_LEVEL_500','MEMBER_LEVEL_2000','MEMBER_LEVEL_5000')";
    public PointRepository(IConfiguration configuration) : base(configuration) { }

    // 写入积分流水（防篡改审计）
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

    // 检查订单对应类型的积分流水是否已经存在。
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

    // 获取所有会员等级（按消费门槛升序）
    public async Task<List<CrmMemberLevel>> GetAllLevelsAsync(IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, async connection =>
            (await connection.QueryAsync<CrmMemberLevel>(
                $"SELECT * FROM Crm_MemberLevels WHERE MemberLevelId IN {ConsumerLevelFilter} ORDER BY MinSpent ASC",
                transaction: transaction)).ToList());
    }

    // 按ID读取会员等级，用于计算本次订单积分倍率
    public async Task<CrmMemberLevel?> GetLevelByIdAsync(
        string memberLevelId,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmMemberLevel>(
                $@"SELECT * FROM Crm_MemberLevels
                  WHERE MemberLevelId = :MemberLevelId AND MemberLevelId IN {ConsumerLevelFilter}",
                new { MemberLevelId = memberLevelId },
                transaction));
    }

    // 按累计消费查询当前应处的最高会员等级
    public async Task<CrmMemberLevel?> GetLevelForSpentAsync(
        decimal totalSpent,
        IDbTransaction? transaction = null)
    {
        return await WithConnectionAsync(transaction, connection =>
            connection.QueryFirstOrDefaultAsync<CrmMemberLevel>(
                $@"SELECT * FROM Crm_MemberLevels
                  WHERE MinSpent <= :TotalSpent AND MemberLevelId IN {ConsumerLevelFilter}
                  ORDER BY MinSpent DESC, MemberLevelId DESC
                  FETCH FIRST 1 ROWS ONLY",
                new { TotalSpent = totalSpent },
                transaction));
    }

    public async Task<List<CrmMemberLevelHistory>> GetMemberLevelHistoryAsync(string customerId)
    {
        return await WithConnectionAsync(null, async connection =>
            (await connection.QueryAsync<CrmMemberLevelHistory>(
                @"SELECT h.HistoryId, h.CustomerId, h.MemberLevelId, l.LevelName,
                         h.QualifiedSpent, h.SettlementMonth, h.CreatedAt
                  FROM Crm_MemberLevelHistories h
                  JOIN Crm_MemberLevels l ON l.MemberLevelId = h.MemberLevelId
                  WHERE h.CustomerId = :CustomerId
                  ORDER BY h.SettlementMonth DESC, h.CreatedAt DESC",
                new { CustomerId = customerId })).ToList());
    }

    public async Task<bool> HasMemberLevelHistoryAsync(string customerId, DateTime settlementMonth, IDbTransaction transaction)
    {
        return await WithConnectionAsync(transaction, async connection =>
            await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM Crm_MemberLevelHistories
                  WHERE CustomerId = :CustomerId AND SettlementMonth = :SettlementMonth",
                new { CustomerId = customerId, SettlementMonth = settlementMonth }, transaction) > 0);
    }

    public Task InsertMemberLevelHistoryAsync(CrmMemberLevelHistory history, IDbTransaction transaction) =>
        WithConnectionAsync(transaction, connection => connection.ExecuteAsync(
            @"INSERT INTO Crm_MemberLevelHistories
                 (HistoryId, CustomerId, MemberLevelId, QualifiedSpent, SettlementMonth, CreatedAt)
              VALUES (:HistoryId, :CustomerId, :MemberLevelId, :QualifiedSpent, :SettlementMonth, SYSDATE)",
            history, transaction));
}
