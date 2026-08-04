using Dapper;
using Microsoft.Extensions.Configuration;
using DBFreshColdChain.Models.DTOs;
using Oracle.ManagedDataAccess.Client;
namespace DBFreshColdChain.Repositories
{
    public class TableLogRepository
    {
        private readonly string? _connectionString;

        public TableLogRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb");
        }

        // 供所有 Service 调用的执行 SQL 方法
        public OracleConnection GetConnection()
        {
            return new OracleConnection(_connectionString);
        }
        public void GroupC_AddLogRecord(GroupC_LogAuditrails logData)
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

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Execute(sql, logData);
            }
        }
    }

}
