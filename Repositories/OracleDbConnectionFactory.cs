using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Repositories;

/// <summary>
/// Oracle 数据库连接工厂实现
/// </summary>
public class OracleDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public OracleDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("未找到 OracleDb 连接字符串");
    }

    public IDbConnection CreateConnection()
        => new OracleConnection(_connectionString);
}
