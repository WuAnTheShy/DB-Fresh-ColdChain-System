using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Services;
using FreshGroupSystem.Services.Order;
using FreshGroupSystem.Services.Leader;
using FreshGroupSystem.Services.Supplier;
using FreshGroupSystem.Repositories;
using FreshGroupSystem.Repositories.Supplier;
using FreshGroupSystem.Repositories.Order;
using FreshGroupSystem.Repositories.Leader;

var builder = WebApplication.CreateBuilder(args);

// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ========== Repository 注册 ==========
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IGroupLeaderRepository, GroupLeaderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();

// ========== Service 注册 ==========
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IGroupLeaderService, GroupLeaderService>();
builder.Services.AddScoped<ILogisticsService, LogisticsService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();

// ========== MVC ==========
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
