using System.Data;

namespace FreshGroupSystem.Data;

/// <summary>
/// 工作单元接口：管理数据库连接和事务，Scoped 生命周期
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }

    /// <summary>打开连接并开始事务</summary>
    Task BeginAsync();

    /// <summary>提交事务</summary>
    Task CommitAsync();

    /// <summary>回滚事务</summary>
    Task RollbackAsync();
}
