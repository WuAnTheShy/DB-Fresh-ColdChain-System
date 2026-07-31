using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// Oracle 数据库连接工厂实现
/// </summary>
public class OracleDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public OracleDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("未找到 DefaultConnection 连接字符串");
    }

    public IDbConnection CreateConnection()
        => new OracleConnection(_connectionString);
}
