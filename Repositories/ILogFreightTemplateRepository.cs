using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 冷链运费模板仓储接口 — 管理 Log_FreightTemplates 表
public interface ILogFreightTemplateRepository : IBaseRepository<LogFreightTemplate>
{
    // 查询所有启用的运费模板
    Task<List<LogFreightTemplate>> GetEnabledAsync();

    // 按温区查询运费模板
    Task<List<LogFreightTemplate>> GetByTemperatureZoneAsync(string zone);
}
