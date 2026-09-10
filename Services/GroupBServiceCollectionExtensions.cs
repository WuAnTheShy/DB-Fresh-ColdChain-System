using FreshColdChain.Controllers.Api;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Services;

// B 组数据访问、业务服务、跨组适配器和后台任务的统一初始化入口。
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
        services.AddScoped<IProductEvaluationRepository, ProductEvaluationRepository>();

        services.AddScoped<IOrderTransactionManager, OracleOrderTransactionManager>();
        services.AddScoped<IPasswordHasher<CrmCustomer>, PasswordHasher<CrmCustomer>>();
        services.AddScoped<IGroupAInventoryGateway, GroupAInventoryServiceAdapter>();
        services.AddScoped<IGroupAProductCatalogService, GroupAProductCatalogService>();
        services.AddGroupALogisticsPersistence(configuration);
        services.AddScoped<ILogisticsService, GroupALogisticsServiceAdapter>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductEvaluationService, ProductEvaluationService>();
        services.AddScoped<ISupplierFulfillmentService, SupplierFulfillmentService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddSingleton<CustomerAuthenticationStateService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IConsumerMessageService, ConsumerMessageService>();
        services.AddScoped<IGroupCPromoterCatalogService, GroupCPromoterCatalogService>();
        services.AddScoped<IConsumerCatalogService, ConsumerCatalogService>();
        services.AddScoped<GroupBApiExceptionFilter>();
        services.Configure<GroupCAuthorizationOptions>(configuration.GetSection(GroupCAuthorizationOptions.SectionName));
        services.AddScoped<IGroupCAuthorizationService, GroupCAuthorizationService>();
        services.AddScoped<GroupBAdminSessionAuthorizationFilter>();
        services.AddScoped<GroupBSupplierSessionAuthorizationFilter>();
        services.AddScoped<GroupBDailyMaintenanceService>();
        services.AddHostedService<GroupBDailyCheckHostedService>();
        services.AddHostedService<GroupBCheckoutExpiryHostedService>();

        return services;
    }
}
