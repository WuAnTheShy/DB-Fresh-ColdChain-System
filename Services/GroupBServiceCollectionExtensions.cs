using FreshColdChain.Controllers.Api;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FreshColdChain.Services;

/// <summary>B 组数据访问、业务服务、跨组适配器和后台任务的统一初始化入口。</summary>
public static class GroupBServiceCollectionExtensions
{
    public static IServiceCollection AddGroupBModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IConsumerMessageRepository, ConsumerMessageRepository>();
        services.AddScoped<IPointRepository, PointRepository>();

        services.AddScoped<IOrderTransactionManager, OracleOrderTransactionManager>();
        services.AddScoped<IPasswordHasher<CrmCustomer>, PasswordHasher<CrmCustomer>>();
        services.AddScoped<IGroupAInventoryGateway, GroupAInventoryServiceAdapter>();
        services.AddScoped<ILogisticsService, GroupALogisticsServiceAdapter>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddSingleton<CustomerAuthenticationStateService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IConsumerMessageService, ConsumerMessageService>();
        services.AddScoped<IGroupCPromoterCatalogService, GroupCPromoterCatalogService>();
        services.AddScoped<GroupBApiExceptionFilter>();
        services.AddScoped<GroupBDailyMaintenanceService>();
        services.AddHostedService<GroupBDailyCheckHostedService>();
        services.AddHostedService<GroupBCheckoutExpiryHostedService>();

        return services;
    }
}
