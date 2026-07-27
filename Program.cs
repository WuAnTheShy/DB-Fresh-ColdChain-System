using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;
using FreshColdChain.Services;

namespace FreshColdChain;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // ========== 依赖注入：注册三层架构的所有组件 ==========
        // 通俗理解："告诉框架，当有人需要 IOrderService 时，给他 OrderService 的实例"

        // Repositories（数据访问层）
        builder.Services.AddScoped<OrderRepository>();
        builder.Services.AddScoped<CustomerRepository>();
        builder.Services.AddScoped<CouponRepository>();
        builder.Services.AddScoped<PointRepository>();

        // Services（业务逻辑层）——对外暴露 Interface
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        builder.Services.AddScoped<ICouponService, CouponService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();


        app.Run();
    }
}
