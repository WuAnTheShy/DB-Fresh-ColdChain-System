using System.Data;

namespace FreshColdChain.Interfaces;

/// <summary>
/// 由 B 组管理订单业务事务的执行器。
/// </summary>
public interface IOrderTransactionManager
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<IDbTransaction, Task<TResult>> operation);

    Task ExecuteAsync(Func<IDbTransaction, Task> operation);
}
