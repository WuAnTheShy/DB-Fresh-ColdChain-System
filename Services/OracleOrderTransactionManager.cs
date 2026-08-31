using System.Data;
using FreshColdChain.Interfaces;
using Oracle.ManagedDataAccess.Client;

namespace FreshColdChain.Services;

/// <summary>
/// Oracle 订单事务执行器。事务的提交和回滚权只属于 B 组业务服务。
/// </summary>
public sealed class OracleOrderTransactionManager : IOrderTransactionManager
{
    private readonly string _connectionString;

    public OracleOrderTransactionManager(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection")
            ?? configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("未配置 OracleConnection/OracleDb 连接字符串");
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<IDbTransaction, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await operation(transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            if (transaction.Connection != null)
                transaction.Rollback();
            throw;
        }
    }

    public async Task ExecuteAsync(Func<IDbTransaction, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteAsync(async transaction =>
        {
            await operation(transaction);
            return true;
        });
    }
}
