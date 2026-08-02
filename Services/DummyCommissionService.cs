using System.Data;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Models.CrossGroup;

namespace FreshGroupSystem.Services;

/// <summary>
/// C 组佣金模块的 Mock 实现 — C 组完成前使用
/// </summary>
public class DummyCommissionService : ICommissionService
{
    public Task RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        // Mock：永远成功，不写 C 组表
        return Task.CompletedTask;
    }
}
