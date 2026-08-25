using FreshColdChain.Interfaces;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using FreshColdChainSystem.Repositories;
using FreshColdChain.Models;
using FreshColdChain.Services.Supplier;
using Microsoft.AspNetCore.Identity;
var builder = WebApplication.CreateBuilder(args);





// ========== Dapper 基础设施 ==========
builder.Services.AddScoped<IDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// ========== Repository 注册 ==========
//A组
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierPriceRepository, SupplierPriceRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockSummaryRepository, StockSummaryRepository>();
builder.Services.AddScoped<IStockBatchRepository, StockBatchRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPriceRuleRepository, PriceRuleRepository>();
// A组冷链专属仓储
builder.Services.AddScoped<ILogFreightTemplateRepository, LogFreightTemplateRepository>();
builder.Services.AddScoped<ILogExpressDeliveryRepository, LogExpressDeliveryRepository>();
builder.Services.AddScoped<ILogFulfillmentBatchItemRepository, LogFulfillmentBatchItemRepository>();
// B组
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<IPointRepository, PointRepository>();
builder.Services.AddScoped<IPromoterRepository, PromoterRepository>();
//C组
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPromoterRepository, PromoterRepository>();
builder.Services.AddScoped<ISysAdminRepository, SysAdminRepository>();
builder.Services.AddScoped<ITableLogRepository, TableLogRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IPromoterSupplierRepository, PromoterSupplierRepository>();
builder.Services.AddScoped<IPCRRepository, PCRRepository>();
// ========== Service 注册 ==========
//A组
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
// A组冷链运费报价与批次级可追溯发货服务。
builder.Services.AddScoped<IColdChainLogisticsService, ColdChainLogisticsService>();
builder.Services.AddScoped<IPricingService, PricingService>();
//B组
builder.Services.AddScoped<IOrderTransactionManager, OracleOrderTransactionManager>();
builder.Services.AddScoped<IPasswordHasher<CrmCustomer>, PasswordHasher<CrmCustomer>>();
builder.Services.AddScoped<IInventoryService, DummyInventoryService>();
builder.Services.AddScoped<ILogisticsService, DummyLogisticsService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICouponService, CouponService>();
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
builder.Services.AddScoped<IRefundService, RefundService>();
//佣金二段结算定时任务：每小时扫描过14天退款期的佣金记录并激活
builder.Services.AddHostedService<GroupC_CommissionSettlementWorker>();
// ========== MVC ==========
builder.Services.AddControllersWithViews();
//B组 API 统一异常过滤器（[ServiceFilter] 要求先注册到 DI）
builder.Services.AddScoped<FreshColdChain.Controllers.Api.GroupBApiExceptionFilter>();
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
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
