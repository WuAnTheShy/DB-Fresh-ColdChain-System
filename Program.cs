using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Services;
using FreshColdChain.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// ========== Repository 注册 ==========
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<PaymentRepository>();
builder.Services.AddScoped<PromoterRepository>();
builder.Services.AddScoped<SysAdminRepository>();
builder.Services.AddScoped<TableLogRepository>();
builder.Services.AddScoped<WithdrawalRepository>();
// ========== Service 注册 ==========
builder.Services.AddScoped<GroupC_PromoterManager>();
builder.Services.AddScoped<GroupC_TableLogManager>();
builder.Services.AddScoped<GroupC_ITableLogManager, GroupC_TableLogManager>();
builder.Services.AddScoped<GroupC_IProMonterManager, GroupC_PromoterManager>();
// ========== MVC ==========
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
// ======== Controller 页面会话 ======== 
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
