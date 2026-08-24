using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 冷链运费模板仓储接口 — 管理 Log_FreightTemplates 表
/// </summary>
public interface ILogFreightTemplateRepository : IBaseRepository<LogFreightTemplate>
{
    /// <summary>查询所有启用的运费模板</summary>
    Task<List<LogFreightTemplate>> GetEnabledAsync();

    /// <summary>按温区查询运费模板</summary>
    Task<List<LogFreightTemplate>> GetByTemperatureZoneAsync(string zone);
}
