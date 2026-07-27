using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Repositories;

/// <summary>
/// 数据库连接基类 - 所有 Repository 继承此类获得 Oracle 连接
/// 你学过的事务 BEGIN/COMMIT/ROLLBACK 在这里用 C# 实现
/// </summary>
public abstract class BaseRepository
{
    private readonly string _connectionString;

    protected BaseRepository(IConfiguration configuration)
    {
        // 从 appsettings.json 读取 Oracle 连接字符串
        _connectionString = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("未配置 OracleConnection 连接字符串");
    }

    /// <summary>
    /// 创建新的数据库连接（每次调用都是新连接）
    /// </summary>
    protected IDbConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }
}
