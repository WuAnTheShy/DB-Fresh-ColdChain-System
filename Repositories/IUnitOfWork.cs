//基础设施类
//工作单元接口（多表操作事务一致性）

using System.Data;

namespace FreshColdChain.Repositories;

// 工作单元接口：管理数据库连接和事务，Scoped 生命周期
// 
// 两种使用模式：
// 1. 内部事务（A组自己用时）：Connection 自动打开 → BeginAsync() → CommitAsync/RollbackAsync
// 2. 外部事务（B组调用时）：AttachExternalTransaction(tx) → 所有操作走 B 组的事务
public interface IUnitOfWork : IDisposable
{
    // 数据库连接（内部事务时自动打开，外部事务时用外部连接）
    IDbConnection Connection { get; }

    // 当前事务
    IDbTransaction? Transaction { get; }

    // 开启内部事务（A组自己写操作时调用）
    Task BeginAsync();

    // 提交内部事务
    Task CommitAsync();

    // 回滚内部事务
    Task RollbackAsync();

    // 挂载外部事务（B组调用时使用）。之后所有操作走这个事务，不再自己管理连接
    void AttachExternalTransaction(IDbTransaction externalTransaction);
}
