using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 工作单元实现：一次 HTTP 请求内共享同一个连接和事务
/// Connection 首次访问时自动打开连接 — 纯读操作无需 BeginAsync
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    /// <summary>
    /// 数据库连接 — 首次访问自动创建并打开
    /// </summary>
    public IDbConnection Connection
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnitOfWork));

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
    /// 开启事务 — 写操作前调用
    /// </summary>
    public async Task BeginAsync()
    {
        // 确保连接已打开
        var conn = Connection;
        if (_transaction != null)
            throw new InvalidOperationException("事务已开启，请勿重复调用");

        _transaction = conn.BeginTransaction();
        await Task.CompletedTask;
    }

    public async Task CommitAsync()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public async Task RollbackAsync()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
