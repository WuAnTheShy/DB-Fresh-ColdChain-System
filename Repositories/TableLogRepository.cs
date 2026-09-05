using Dapper;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using FreshColdChain.Models;
using System.Data;

namespace FreshColdChain.Repositories
{
    public class TableLogRepository:ITableLogRepository
    {
        private readonly string _connectionString;

        public TableLogRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleConnection")
                ?? throw new InvalidOperationException("未配置 OracleConnection 连接字符串");
        }

        // 创建独立数据库连接
        private OracleConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        // 异步添加审计日志记录
        public async Task GroupC_AddLogRecordAsync(
            GroupC_LogAuditrails logData,
            CancellationToken cancellationToken = default,
            IDbTransaction? transaction = null)
        {
            string sql = @"
                INSERT INTO LOG_AUDITTRAILS (
                    LOGID, 
                    TABLENAME, 
                    RECORDID, 
                    ACTIONTYPE, 
                    OLDVALUE, 
                    NEWVALUE, 
                    OPERATORTYPE, 
                    OPERATORID, 
                    OPTIME
                ) VALUES (
                    :LogId, 
                    :TableName, 
                    :RecordId, 
                    :ActionType,
                    :OldValue, 
                    :NewValue, 
                    :OperatorType, 
                    :OperatorId, 
                    :OpTime
                )";

            cancellationToken.ThrowIfCancellationRequested();
            if (transaction != null)
            {
                var sharedConnection = transaction.Connection;
                if (sharedConnection?.State != ConnectionState.Open)
                    throw new InvalidOperationException("审计外部事务已失效");
                await sharedConnection.ExecuteAsync(new CommandDefinition(
                    sql, logData, transaction, cancellationToken: cancellationToken));
                return;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql, logData, cancellationToken: cancellationToken));
        }

        // 组合查询操作日志（管理端查询页用，结果上限 500 条）
        public async Task<List<GroupC_LogAuditrails>> SearchAsync(DateTime? startTime, DateTime? endTime,
            string? tableName, string? actionType, string? operatorId)
        {
            const string sql = @"
                SELECT * FROM LOG_AUDITTRAILS
                WHERE (:StartTime IS NULL OR OPTIME >= :StartTime)
                  AND (:EndTime IS NULL OR OPTIME < :EndTime)
                  AND (:TableName IS NULL OR TABLENAME = :TableName)
                  AND (:ActionType IS NULL OR ACTIONTYPE = :ActionType)
                  AND (:OperatorId IS NULL OR OPERATORID = :OperatorId)
                ORDER BY OPTIME DESC
                FETCH FIRST 500 ROWS ONLY";
            await using var connection = CreateConnection();
            var result = await connection.QueryAsync<GroupC_LogAuditrails>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime,
                TableName = string.IsNullOrWhiteSpace(tableName) ? null : tableName,
                ActionType = string.IsNullOrWhiteSpace(actionType) ? null : actionType,
                OperatorId = string.IsNullOrWhiteSpace(operatorId) ? null : operatorId.Trim()
            });
            return result.ToList();
        }

        // 查询日志中出现过的全部表名（筛选下拉用）
        public async Task<List<string>> GetDistinctTableNamesAsync()
        {
            const string sql = "SELECT DISTINCT TABLENAME FROM LOG_AUDITTRAILS ORDER BY TABLENAME";
            await using var connection = CreateConnection();
            var result = await connection.QueryAsync<string>(sql);
            return result.ToList();
        }
    }
}
