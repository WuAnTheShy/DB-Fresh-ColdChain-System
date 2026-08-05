using Dapper;
using Microsoft.Extensions.Configuration;
using DBFreshColdChain.Models.DTOs;
using Oracle.ManagedDataAccess.Client;

namespace DBFreshColdChain.Repositories
{
    public class TableLogRepository:ITableLogRepository
    {
        private readonly string _connectionString;

        public TableLogRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb")
                ?? throw new InvalidOperationException("未配置 OracleDb 连接字符串");
        }

        // 创建独立数据库连接
        private OracleConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        // 异步添加审计日志记录
        public async Task GroupC_AddLogRecordAsync(
            GroupC_LogAuditrails logData,
            CancellationToken cancellationToken = default)
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

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(sql, logData);
        }
    }
}