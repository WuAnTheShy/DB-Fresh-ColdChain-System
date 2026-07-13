using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
public class DbHelper
{
    private readonly string _connectionString;
    public DbHelper(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("OracleDb");
    }

    // 供所有 Service 调用的执行 SQL 方法
    public OracleConnection GetConnection()
    {
        return new OracleConnection(_connectionString);
    }
}