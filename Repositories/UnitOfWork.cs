using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 工作单元实现：一次 HTTP 请求内共享同一个连接和事务
///
/// 内部事务：Connection 惰性打开 → BeginAsync 开启事务
/// 外部事务：B 组调用 AttachExternalTransaction 挂载后，所有操作走 B 组的事务
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;
    private bool _externalMode;

    public IDbConnection Connection
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnitOfWork));

            if (_externalMode && _transaction != null)
                return _transaction.Connection
                    ?? throw new InvalidOperationException("外部事务已结束");

            if (_connection == null)
            {
                _connection = _connectionFactory.CreateConnection();
                _connection.Open();
            }
            return _connection;
        }
    }

    public IDbTransaction? Transaction => _transaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// 挂载外部事务 — B 组调用 A 组接口时使用。
    /// 挂载后 Connection 和 Transaction 都由外部管理，BeginAsync/CommitAsync/RollbackAsync 变为空操作。
    /// </summary>
    public void AttachExternalTransaction(IDbTransaction externalTransaction)
    {
        ArgumentNullException.ThrowIfNull(externalTransaction);

        if (externalTransaction.Connection == null)
            throw new ArgumentException("外部事务未关联数据库连接");

        _externalMode = true;
        _transaction = externalTransaction;
    }

    /// <summary>
    /// 开启内部事务 — A 组自己的写操作使用
    /// </summary>
    public async Task BeginAsync()
    {
        if (_externalMode) return; // 外部模式不自己开事务

        var conn = Connection;
        if (_transaction != null)
            throw new InvalidOperationException("事务已开启");

        _transaction = conn.BeginTransaction();
        await Task.CompletedTask;
    }

    public async Task CommitAsync()
    {
        if (_externalMode) return; // 外部模式不自己提交

        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public async Task RollbackAsync()
    {
        if (_externalMode) return; // 外部模式不自己回滚

        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (!_externalMode)
            {
                _transaction?.Dispose();
                _connection?.Dispose();
            }
            _disposed = true;
        }
    }
}
