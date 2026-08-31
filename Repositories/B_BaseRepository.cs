using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Repositories;

/// <summary>
/// 数据库连接基类 - 所有 Repository 继承此类获得 Oracle 连接
/// 你学过的事务 BEGIN/COMMIT/ROLLBACK 在这里用 C# 实现
/// </summary>
public abstract class B_BaseRepository
{
    private readonly string _connectionString;

    protected B_BaseRepository(IConfiguration configuration)
    {
        // 生产和开发环境均应通过 Secret 或环境变量注入连接字符串。
        var connectionString = configuration.GetConnectionString("OracleConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "未配置 OracleConnection；请设置环境变量 ConnectionStrings__OracleConnection");
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// 创建新的数据库连接（每次调用都是新连接）
    /// </summary>
    protected IDbConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }

    /// <summary>
    /// 在指定事务所属连接上执行数据库操作。
    /// 未传入事务时，由仓储自行创建、打开并释放连接。
    /// </summary>
    protected async Task<TResult> WithConnectionAsync<TResult>(
        IDbTransaction? transaction,
        Func<IDbConnection, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (transaction != null)
        {
            var transactionConnection = transaction.Connection
                ?? throw new InvalidOperationException("事务已结束或未关联数据库连接");

            if (transactionConnection.State != ConnectionState.Open)
                throw new InvalidOperationException("事务关联的数据库连接未打开");

            return await operation(transactionConnection);
        }

        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        return await operation(connection);
    }

    /// <summary>
    /// 在指定事务所属连接上执行无返回值的数据库操作。
    /// </summary>
    protected async Task WithConnectionAsync(
        IDbTransaction? transaction,
        Func<IDbConnection, Task> operation)
    {
        await WithConnectionAsync(transaction, async connection =>
        {
            await operation(connection);
            return true;
        });
    }
}
