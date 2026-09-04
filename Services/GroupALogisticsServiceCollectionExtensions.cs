using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

/// <summary>A 组物流存储选择；生产环境禁止内存降级，不自动执行数据库迁移。</summary>
public static class GroupALogisticsServiceCollectionExtensions
{
    public static IServiceCollection AddGroupALogisticsPersistence(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DemoCarrierOptions>(configuration.GetSection(DemoCarrierOptions.SectionName));
        services.AddScoped<IGroupACarrierRepository, GroupACarrierRepository>();
        services.AddScoped<GroupADemoCarrierService>();
        services.AddScoped<FreshColdChain.Controllers.Api.DemoCarrierAuthorizationFilter>();
        services.AddOptions<GroupALogisticsOptions>()
            .Bind(configuration.GetSection(GroupALogisticsOptions.SectionName))
            .Validate(options => options.Provider is "Oracle" or "Fallback", "物流 Provider 只支持 Oracle 或 Fallback")
            .Validate<IHostEnvironment>((options, environment) => options.Provider != "Fallback" || environment.IsDevelopment(),
                "内存物流兜底仅允许在 Development 环境显式启用")
            .Validate(options => options.ChilledMinimumCelsius <= options.ChilledMaximumCelsius,
                "冷藏温度下限不能高于上限")
            .ValidateOnStart();
        services.AddOptions<GroupALogisticsFallbackOptions>()
            .Bind(configuration.GetSection(GroupALogisticsFallbackOptions.SectionName))
            .Validate<IOptions<GroupALogisticsOptions>>((options, selected) => selected.Value.Provider != "Fallback" ||
                (!string.IsNullOrWhiteSpace(options.CarrierCode) && !string.IsNullOrWhiteSpace(options.CarrierName) &&
                 !string.IsNullOrWhiteSpace(options.OriginLocation) && !string.IsNullOrWhiteSpace(options.ShippedDescription) &&
                 !string.IsNullOrWhiteSpace(options.DelayDescription) && options.EstimatedTransitHours > 0 &&
                 options.ChilledMinimumCelsius <= options.ChilledMaximumCelsius), "物流兜底配置不完整")
            .ValidateOnStart();
        services.AddScoped<IGroupALogisticsRepository, GroupALogisticsRepository>();
        services.AddScoped<OracleGroupALogisticsExtensionProvider>();
        services.AddSingleton<FallbackGroupALogisticsExtensionProvider>();
        services.AddScoped<IGroupALogisticsExtensionProvider>(provider =>
            provider.GetRequiredService<IOptions<GroupALogisticsOptions>>().Value.Provider == "Oracle"
                ? provider.GetRequiredService<OracleGroupALogisticsExtensionProvider>()
                : provider.GetRequiredService<FallbackGroupALogisticsExtensionProvider>());
        return services;
    }
}
