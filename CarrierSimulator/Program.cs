using CarrierSimulator.Services;
using Microsoft.Extensions.Options;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddOptions<CarrierClientOptions>().Bind(builder.Configuration.GetSection("CarrierClient"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
        uri.IsLoopback && uri.Scheme is "http" or "https", "演示后端必须使用本机 HTTP/HTTPS 地址")
    .Validate(options => options.ApiKey.Length >= 32, "请使用演示启动脚本设置物流商凭据")
    .ValidateOnStart();
builder.Services.AddHttpClient<CarrierClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<CarrierClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-Carrier-Key", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(20);
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    // 演示器含写权限，只允许本机访问；密钥仅在服务端发往商城。
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
        context.Request.Host.Host.ToLowerInvariant() is not ("localhost" or "127.0.0.1" or "[::1]" or "::1"))
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.UseStaticFiles();
app.MapGet("/lib/bootstrap.min.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "bootstrap.min.css"), "text/css"));
app.MapControllerRoute("default", "{controller=Shipments}/{action=Index}/{id?}");
app.Run();
