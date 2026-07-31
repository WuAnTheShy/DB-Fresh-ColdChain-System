using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 工作单元实现：一次 HTTP 请求内共享同一个连接和事务
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public IDbConnection Connection
        => _connection ?? throw new InvalidOperationException("请先调用 BeginAsync 开启事务");

    public IDbTransaction? Transaction => _transaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task BeginAsync()
    {
        if (_connection != null)
            throw new InvalidOperationException("事务已开启，请勿重复调用 BeginAsync");

        _connection = _connectionFactory.CreateConnection();
        _connection.Open();
        _transaction = _connection.BeginTransaction();
        await Task.CompletedTask;
    }

    public async Task CommitAsync()
    {
        _transaction?.Commit();
        await Task.CompletedTask;
    }

    public async Task RollbackAsync()
    {
        _transaction?.Rollback();
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
