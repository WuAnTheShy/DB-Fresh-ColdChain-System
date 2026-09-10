using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Repositories;

// Oracle 数据库连接工厂实现
public class OracleDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public OracleDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("未找到 OracleConnection 连接字符串");
    }

    public IDbConnection CreateConnection()
        => new OracleConnection(_connectionString);
}
