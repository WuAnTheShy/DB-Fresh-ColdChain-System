//基础设施类
//工作单元接口（多表操作事务一致性）

using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 工作单元接口：管理数据库连接和事务，Scoped 生命周期
/// Connection 首次访问自动打开 — 读操作无需 BeginAsync
/// BeginAsync 仅用于需要事务的写操作
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>数据库连接（首次访问自动打开）</summary>
    IDbConnection Connection { get; }

    /// <summary>当前事务（调用 BeginAsync 后才非空）</summary>
    IDbTransaction? Transaction { get; }

    /// <summary>开启事务（写操作前调用）</summary>
    Task BeginAsync();

    /// <summary>提交事务</summary>
    Task CommitAsync();

    /// <summary>回滚事务</summary>
    Task RollbackAsync();
}
