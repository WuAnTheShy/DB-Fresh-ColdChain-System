using System.Data;
using FreshGroupSystem.Models.CrossGroup;

namespace FreshGroupSystem.Interfaces;

/// <summary>
/// B 组调用 C 组佣金模块的跨组契约（C 组还未实现，目前用 Dummy）
/// </summary>
public interface ICommissionService
{
    Task RegisterCompletedOrderAsync(
        CommissionOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
