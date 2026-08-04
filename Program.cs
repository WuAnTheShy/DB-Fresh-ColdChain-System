using DBFreshColdChain.Interfaces;
using DBFreshColdChain.Repositories;
using DBFreshColdChain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<PromoterManager>();
builder.Services.AddScoped<DbHelper>();
builder.Services.AddScoped<TableLogManager>();
builder.Services.AddScoped<GroupC_ITableLogManager, TableLogManager>();
builder.Services.AddScoped<GroupC_IProMonterManager, PromoterManager>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
//builder.Services.AddHttpContextAccessor(); // зЂВс IHttpContextAccessor
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
