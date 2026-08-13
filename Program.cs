using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Services;
using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;
using FreshColdChain.Services.Supplier;

var builder = WebApplication.CreateBuilder(args);


// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// ========== Repository 注册 ==========
//A组
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();


//C组
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPromoterRepository, PromoterRepository>();
builder.Services.AddScoped<ISysAdminRepository, SysAdminRepository>();
builder.Services.AddScoped<ITableLogRepository, TableLogRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
// ========== Service 注册 ==========
//A组
builder.Services.AddScoped<ISupplierService, SupplierService>();

//C组
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<WithdrawalService>();
builder.Services.AddScoped<PromoterService>();
builder.Services.AddScoped<SystemAdminService>();
builder.Services.AddScoped<ITableLogService, TableLogService>();
builder.Services.AddScoped<ICommissionService, CommissionService> ();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPromoterService, PromoterService>();
builder.Services.AddScoped<PromoterPortalDataProvider>();
//builder.Services.AddScoped<IRefundService, RefundService>();
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
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
