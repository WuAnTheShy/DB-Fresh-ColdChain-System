using FreshColdChain.Interfaces;
using FreshColdChain.Services;
using FreshColdChain.Services.Supplier;
using FreshColdChain.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ========== Repository 注册 ==========
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockSummaryRepository, StockSummaryRepository>();
builder.Services.AddScoped<IStockBatchRepository, StockBatchRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPriceRuleRepository, PriceRuleRepository>();
// A组冷链专属仓储
builder.Services.AddScoped<ILogFreightTemplateRepository, LogFreightTemplateRepository>();
builder.Services.AddScoped<ILogExpressDeliveryRepository, LogExpressDeliveryRepository>();
builder.Services.AddScoped<ILogFulfillmentBatchItemRepository, LogFulfillmentBatchItemRepository>();

// ========== Service 注册 ==========
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
// A组冷链运费报价与批次级可追溯发货服务。
builder.Services.AddScoped<IColdChainLogisticsService, ColdChainLogisticsService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();

// ========== B 组跨组接口适配器 ==========
builder.Services.AddScoped<IInventoryService, InventoryServiceAdapter>();
builder.Services.AddScoped<ILogisticsService, LogisticsServiceAdapter>();
builder.Services.AddScoped<ICommissionService, DummyCommissionService>();

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
