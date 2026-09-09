using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using FreshColdChain.Services.Supplier;
using FreshColdChainSystem.Repositories;
using System.Text.Json.Serialization;
using Oracle.ManagedDataAccess.Client;

// Oracle 参数按名称绑定（Dapper 命名参数依赖此设置，否则按位置绑定会绑错参数）
OracleConfiguration.BindByName = true;

var builder = WebApplication.CreateBuilder(args);

// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ========== Repository 注册 ==========
// A组
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierPriceRepository, SupplierPriceRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockSummaryRepository, StockSummaryRepository>();
builder.Services.AddScoped<IStockBatchRepository, StockBatchRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPriceRuleRepository, PriceRuleRepository>();
builder.Services.AddScoped<IGoodsRepository, GoodsRepository>();
builder.Services.AddScoped<ILogFreightTemplateRepository, LogFreightTemplateRepository>();
builder.Services.AddScoped<ILogExpressDeliveryRepository, LogExpressDeliveryRepository>();
builder.Services.AddScoped<ILogFulfillmentBatchItemRepository, LogFulfillmentBatchItemRepository>();

// C组
builder.Services.AddScoped<IPromoterRepository, PromoterRepository>();
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISysAdminRepository, SysAdminRepository>();
builder.Services.AddScoped<ITableLogRepository, TableLogRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IPromoterSupplierRepository, PromoterSupplierRepository>();
builder.Services.AddScoped<IPromoterProductRepository, PromoterProductRepository>();
builder.Services.AddScoped<IPCRRepository, PCRRepository>();
// ========== Service 注册 ==========
// A组
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
builder.Services.AddScoped<IColdChainLogisticsService, ColdChainLogisticsService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IGoodsService, GoodsService>();

// B组
builder.Services.AddGroupBModule(builder.Configuration);

// C组
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<WithdrawalService>();
builder.Services.AddScoped<PromoterIntroStore>();
builder.Services.AddScoped<PromoterService>();
builder.Services.AddScoped<SystemAdminService>();
builder.Services.AddScoped<ITableLogService, TableLogService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPromoterService, PromoterService>();
builder.Services.AddScoped<PromoterPortalDataProvider>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IPromoterListedPriceSyncService, PromoterListedPriceSyncService>();
builder.Services.AddHostedService<GroupC_CommissionSettlementWorker>();

// ========== MVC / API ==========
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseMiddleware<CustomerSessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapFallbackToFile(
    "/app/{*path:nonfile}",
    "app/index.html");

app.Run();
