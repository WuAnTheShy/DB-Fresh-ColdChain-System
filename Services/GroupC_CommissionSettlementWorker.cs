using FreshColdChain.Interfaces;
using FreshColdChain.Models.CrossGroup_C;
using FreshColdChain.Repositories;

namespace FreshColdChain.Services
{
    /// <summary>
    /// 佣金二段结算定时任务：每小时扫描一次已过14天退款期、仍为 Pending 的佣金记录，
    /// 逐条调用 ActivatePromoterMoney 完成激活（待结算余额 → 可提现余额）。
    /// </summary>
    public class GroupC_CommissionSettlementWorker : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GroupC_CommissionSettlementWorker> _logger;

        public GroupC_CommissionSettlementWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<GroupC_CommissionSettlementWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 启动时先补扫一次，避免应用停机期间过期的记录滞留
            await SettleDueCommissionsAsync(stoppingToken);

            using var timer = new PeriodicTimer(ScanInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SettleDueCommissionsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 应用关闭，正常退出
            }
        }

        private async Task SettleDueCommissionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // UnitOfWork / 仓储均为 Scoped，必须显式创建作用域
                using var scope = _scopeFactory.CreateScope();
                var commissionRepository = scope.ServiceProvider.GetRequiredService<ICommissionRepository>();
                var commissionService = scope.ServiceProvider.GetRequiredService<ICommissionService>();

                var dueRecords = await commissionRepository.GetDueSettlementsAsync(DateTime.Now);
                if (dueRecords.Count > 0)
                {
                    _logger.LogInformation("佣金二段结算：发现 {Count} 条到期待结算记录", dueRecords.Count);
                }

                foreach (var record in dueRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // 每条记录独立事务（transaction 传 null），单条失败不影响其他记录
                        var result = await commissionService.ActivatePromoterMoney(
                            new ActivateCommissionOrderRequest
                            {
                                orderID = record.OrderId,
                                promoterID = record.PromoterId,
                                commBaseAmount = record.CommBaseAmount,
                                commBonusAmount = record.CommBonusAmount
                            },
                            null,
                            cancellationToken);

                        if (result.IsSuccess)
                        {
                            _logger.LogInformation(
                                "佣金已激活：订单 {OrderId}，团长 {PromoterId}",
                                record.OrderId, record.PromoterId);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "佣金激活失败：订单 {OrderId}，原因：{Error}",
                                record.OrderId, result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "佣金激活异常：订单 {OrderId}", record.OrderId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "佣金二段结算扫描失败");
            }
        }
    }
}
