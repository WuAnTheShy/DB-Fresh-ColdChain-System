<<<<<<< HEAD
using Microsoft.EntityFrameworkCore;
using FreshGroupSystem.Data;
using FreshGroupSystem.Interfaces;
using FreshGroupSystem.Services;
using FreshGroupSystem.Services.Order;
using FreshGroupSystem.Services.Leader;
using FreshGroupSystem.Repositories;
using FreshGroupSystem.Repositories.Supplier;
using FreshGroupSystem.Repositories.Order;
using FreshGroupSystem.Repositories.Leader;

var builder = WebApplication.CreateBuilder(args);

// ========== 数据库 ==========
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));

// ========== Repository 注册 ==========
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IGroupLeaderRepository, GroupLeaderRepository>();

// ========== Service 注册 ==========
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IGroupLeaderService, GroupLeaderService>();
builder.Services.AddScoped<ILogisticsService, LogisticsService>();

// ========== 控制器 + Swagger ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== 跨域 ==========
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
=======
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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


>>>>>>> 426d4f8de3687c9f651feb2f1c38d1fcd5444391
app.Run();
