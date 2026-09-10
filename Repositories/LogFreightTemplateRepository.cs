using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

// 冷链运费模板仓储实现
public class LogFreightTemplateRepository : BaseRepository<LogFreightTemplate>, ILogFreightTemplateRepository
{
    public LogFreightTemplateRepository(IUnitOfWork uow) : base(uow) { }

    // 查询所有启用的运费模板
    public async Task<List<LogFreightTemplate>> GetEnabledAsync()
    {
        var sql = """SELECT * FROM Log_FreightTemplates WHERE IsEnabled = 1 ORDER BY TemplateID """;
        return (await _uow.Connection.QueryAsync<LogFreightTemplate>(sql, transaction: _uow.Transaction)).ToList();
    }

    // 按温区查询运费模板
    public async Task<List<LogFreightTemplate>> GetByTemperatureZoneAsync(string zone)
    {
        var sql = """SELECT * FROM Log_FreightTemplates WHERE TemperatureZone = :Zone AND IsEnabled = 1 ORDER BY TemplateID """;
        return (await _uow.Connection.QueryAsync<LogFreightTemplate>(sql, new { Zone = zone }, _uow.Transaction)).ToList();
    }
}
