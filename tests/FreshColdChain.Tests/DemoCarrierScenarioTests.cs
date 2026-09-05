using FreshColdChain.Controllers.Api;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Tests;

internal static class DemoCarrierScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var failures = 0;
        var cases = new (string Name, Func<Task> Run)[] {
            ("物流商接口默认关闭、生产禁用及密钥校验", AuthorizationAsync),
            ("物流商只使用数据库中的订单供应商身份并提交同一事务", () => TransactionAsync("success")),
            ("物流商越权运单拒绝且回滚", () => TransactionAsync("missing")),
            ("物流事件业务失败回滚", () => TransactionAsync("failure"))
        };
        foreach (var (name, run) in cases)
        {
            try { await run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception exception) { failures++; Console.WriteLine($"FAIL {name}: {exception}"); }
        }
        return failures == 0 ? 0 : 1;
    }

    private static async Task AuthorizationAsync()
    {
        var key = new string('k', 64);
        foreach (var scenario in new[] { "disabled", "production", "fallback", "empty-scope", "missing", "wrong", "valid", "all-database" })
        {
            var settings = new DemoCarrierOptions { Enabled = scenario != "disabled", ApiKey = key,
                IncludeAllDatabaseShipments = scenario == "all-database",
                SupplierIds = scenario is "empty-scope" or "all-database" ? [] : ["SUP-DEMO"] };
            var environment = FinancialProxy.Create<IHostEnvironment>((_, _) => scenario == "production" ? "Production" : "Development");
            var filter = new DemoCarrierAuthorizationFilter(Options.Create(settings),
                Options.Create(new GroupALogisticsOptions { Provider = scenario == "fallback" ? "InMemory" : "Oracle" }), environment);
            var context = new AuthorizationFilterContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []);
            if (scenario != "missing") context.HttpContext.Request.Headers["X-Carrier-Key"] = scenario == "wrong" ? "invalid" : key;
            await filter.OnAuthorizationAsync(context);
            if (scenario is "valid" or "all-database") AssertEx.True(context.Result == null);
            else if (scenario is "disabled" or "production" or "fallback") AssertEx.True(context.Result is NotFoundResult);
            else AssertEx.True(context.Result is UnauthorizedObjectResult);
        }
    }

    private static async Task TransactionAsync(string scenario)
    {
        var uow = new FinancialUnitOfWork();
        var events = 0;
        var repository = FinancialProxy.Create<IGroupACarrierRepository>((_, args) =>
        {
            AssertEx.Equal("DEL-DEMO", (string)args![0]!);
            AssertEx.True((bool)args![1]!);
            AssertEx.Equal(0, ((string[])args[2]!).Length);
            AssertEx.True(ReferenceEquals(uow.Transaction, args[3]));
            return Task.FromResult<LogExpressDelivery?>(scenario == "missing" ? null : new() { OrderID = "ORDER-DB", SupplierID = "SUP-DEMO" });
        });
        var logistics = FinancialProxy.Create<IGroupALogisticsExtensionProvider>((_, args) =>
        {
            events++;
            var command = (LogisticsTrackingEventCommand)args![0]!;
            AssertEx.Equal("ORDER-DB", command.OrderId);
            AssertEx.Equal("SUP-DEMO", command.SupplierId);
            AssertEx.True(ReferenceEquals(uow.Transaction, args[1]));
            if (scenario == "failure") throw new InvalidOperationException("模拟写入失败");
            return Task.FromResult(new SupplierLogisticsSnapshot());
        });
        var service = new GroupADemoCarrierService(uow, repository, logistics,
            Options.Create(new DemoCarrierOptions { IncludeAllDatabaseShipments = true }));
        var failed = false;
        try { var result = await service.AppendAsync("DEL-DEMO", new(), CancellationToken.None); AssertEx.True((result == null) == (scenario == "missing")); }
        catch (InvalidOperationException) when (scenario == "failure") { failed = true; }
        AssertEx.True(failed == (scenario == "failure"));
        AssertEx.Equal(scenario == "missing" ? 0 : 1, events);
        AssertEx.True(uow.Transaction == null);
        AssertEx.True(uow.LastTransaction!.Committed == (scenario == "success"));
        AssertEx.True(uow.LastTransaction.RolledBack == (scenario != "success"));
    }
}
