using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;

namespace FreshColdChain;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddScoped<Controllers.Api.GroupBApiExceptionFilter>();

        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
        builder.Services.AddScoped<ICouponRepository, CouponRepository>();
        builder.Services.AddScoped<IPointRepository, PointRepository>();
        builder.Services.AddScoped<IPromoterRepository, PromoterRepository>();

        builder.Services.AddScoped<IOrderTransactionManager, OracleOrderTransactionManager>();
        builder.Services.AddScoped<IPasswordHasher<CrmCustomer>, PasswordHasher<CrmCustomer>>();
        builder.Services.AddScoped<IInventoryService, DummyInventoryService>();
        builder.Services.AddScoped<ILogisticsService, DummyLogisticsService>();
        builder.Services.AddScoped<ICommissionService, DummyCommissionService>();
        builder.Services.AddScoped<IGroupCInterface, GroupCInterfaceService>();
        builder.Services.AddScoped<GroupBDailyMaintenanceService>();
        builder.Services.AddHostedService<GroupBDailyCheckHostedService>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        builder.Services.AddScoped<ICouponService, CouponService>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
