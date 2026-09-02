using FreshColdChain.Controllers.Api;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Services;

/// <summary>B 组数据访问、业务服务、跨组适配器和后台任务的统一初始化入口。</summary>
public static class GroupBServiceCollectionExtensions
{
    public static IServiceCollection AddGroupBModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IConsumerMessageRepository, ConsumerMessageRepository>();
        services.AddScoped<IPointRepository, PointRepository>();

        services.AddScoped<IOrderTransactionManager, OracleOrderTransactionManager>();
        services.AddScoped<IPasswordHasher<CrmCustomer>, PasswordHasher<CrmCustomer>>();
        services.AddScoped<IGroupAInventoryGateway, GroupAInventoryServiceAdapter>();
        services.AddScoped<IGroupAProductCatalogService, GroupAProductCatalogService>();
        services.AddOptions<GroupALogisticsFallbackOptions>()
            .Bind(configuration.GetSection(GroupALogisticsFallbackOptions.SectionName))
            .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.CarrierCode) &&
                    !string.IsNullOrWhiteSpace(options.CarrierName) &&
                    !string.IsNullOrWhiteSpace(options.OriginLocation) &&
                    !string.IsNullOrWhiteSpace(options.ShippedDescription) &&
                    options.EstimatedTransitHours > 0,
                "B 组物流兜底配置不完整")
            .ValidateOnStart();
        services.AddSingleton<IGroupALogisticsExtensionProvider,
            FallbackGroupALogisticsExtensionProvider>();
        services.AddScoped<ILogisticsService, GroupALogisticsServiceAdapter>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddSingleton<CustomerAuthenticationStateService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IConsumerMessageService, ConsumerMessageService>();
        services.AddScoped<IGroupCPromoterCatalogService, GroupCPromoterCatalogService>();
        services.AddScoped<IConsumerCatalogService, ConsumerCatalogService>();
        services.AddScoped<GroupBApiExceptionFilter>();
        services.AddScoped<GroupBAdminSessionAuthorizationFilter>();
        services.AddScoped<GroupBDailyMaintenanceService>();
        services.AddHostedService<GroupBDailyCheckHostedService>();
        services.AddHostedService<GroupBCheckoutExpiryHostedService>();

        return services;
    }
}
