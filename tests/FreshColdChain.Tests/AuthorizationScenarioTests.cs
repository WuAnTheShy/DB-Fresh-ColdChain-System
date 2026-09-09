using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FreshColdChain.Controllers;
using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Models.DTOs;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Tests;

internal static class AuthorizationScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("角色读写权限分离且未知权限默认拒绝", RolePermissionsAsync),
            ("管理员或角色被停用后旧会话不能继续授权", RevocationAsync),
            ("供应商授权验证自身范围状态及资质", SupplierPermissionsAsync),
            ("管理员过滤器拒绝旧名称会话并执行动作权限", AdminFilterAsync),
            ("供应商过滤器使用服务端身份及独立读写权限", SupplierFilterAsync),
            ("授权异常或身份不匹配不能放行", FailureClosedAsync),
            ("所有B组后台动作必须明确声明权限", AllActionsDeclarePermissionsAsync),
            ("管理员登录只接受启用账号并建立稳定ID会话", AdminLoginAsync)
        };
        var failures = 0;
        foreach (var (name, run) in scenarios)
        {
            try { await run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception exception) { failures++; Console.WriteLine($"FAIL {name}\n{exception}"); }
        }
        Console.WriteLine($"跨组权限场景总数: {scenarios.Length}, 通过: {scenarios.Length - failures}, 失败: {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static GroupC_SysUser User() => new() { UserId = "ADMIN1", RoleId = "r_admin", Status = "Enabled" };
    private static GroupC_SysRole Role() => new() { RoleId = "r_admin", RoleCode = "ADMIN", Status = "Enabled" };
    private static GroupCAuthorizationOptions Grants(params string[] adminGrants) => new()
    {
        RolePermissions = new() { ["r_admin"] = adminGrants },
        SupplierPermissions = [GroupBPermissions.FulfillmentRead, GroupBPermissions.FulfillmentWrite]
    };
    private static ISysAdminRepository Admins(GroupC_SysUser? user, GroupC_SysRole? role) =>
        FinancialProxy.Create<ISysAdminRepository>((method, _) => method.Name switch
        {
            nameof(ISysAdminRepository.GetUserByIdAsync) => Task.FromResult(user),
            nameof(ISysAdminRepository.GetRoleByIdAsync) => Task.FromResult(role),
            nameof(ISysAdminRepository.GetUserByName) => user,
            _ => throw new NotSupportedException(method.Name)
        });
    private static GroupCAuthorizationService Service(GroupC_SysUser? user, GroupC_SysRole? role,
        GroupCAuthorizationOptions options, ISupplierService? suppliers = null) => new(Admins(user, role),
        suppliers ?? FinancialProxy.Create<ISupplierService>(), new AuthorizationOptionsSnapshot(options));

    private static async Task RolePermissionsAsync()
    {
        var options = Grants(GroupBPermissions.OrdersRead);
        var role = Role();
        var user = User();
        var service = Service(user, role, options);
        AssertEx.True((await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersRead)).IsAllowed);
        AssertEx.True(!(await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersManage)).IsAllowed);
        AssertEx.True(!(await service.AuthorizeAsync(user.UserId, "unknown")).IsAllowed);
        AssertEx.True(!(await service.AuthorizeAsync("", GroupBPermissions.OrdersRead)).IsAllowed);
        user.RoleId = role.RoleId = "unconfigured-role";
        AssertEx.True(!(await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersRead)).IsAllowed);
    }

    private static async Task RevocationAsync()
    {
        var user = User();
        var role = Role();
        var service = Service(user, role, Grants(GroupBPermissions.OrdersRead));
        AssertEx.True((await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersRead)).IsAllowed);
        foreach (var status in new[] { "Disabled", "Disable", "Locked", "Pending", "" })
        {
            user.Status = status;
            AssertEx.True(!(await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersRead)).IsAllowed);
            user.Status = "Enabled";
            role.Status = status;
            AssertEx.True(!(await service.AuthorizeAsync(user.UserId, GroupBPermissions.OrdersRead)).IsAllowed);
            role.Status = "Enabled";
        }
        AssertEx.True(!(await Service(null, role, Grants(GroupBPermissions.OrdersRead))
            .AuthorizeAsync("ADMIN1", GroupBPermissions.OrdersRead)).IsAllowed);
        AssertEx.True(!(await Service(user, null, Grants(GroupBPermissions.OrdersRead))
            .AuthorizeAsync("ADMIN1", GroupBPermissions.OrdersRead)).IsAllowed);
    }

    private static async Task SupplierPermissionsAsync()
    {
        var supplier = new SupplierDto { SupplierID = "SUP1", Status = "Active", ExpiryDate = DateTime.Now.AddDays(5) };
        var suppliers = FinancialProxy.Create<ISupplierService>((method, args) =>
        {
            AssertEx.Equal(nameof(ISupplierService.GetSupplierByIdAsync), method.Name);
            AssertEx.Equal("SUP1", (string)args![0]!);
            return Task.FromResult(ApiResponse<SupplierDto>.Success(supplier));
        });
        var options = Grants();
        var service = Service(null, null, options, suppliers);
        AssertEx.True((await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentWrite, "SUP1")).IsAllowed);
        AssertEx.True(!(await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentRead, "SUP2")).IsAllowed);
        foreach (var status in new[] { "Disabled", "Pending", "Rejected", "" })
        {
            supplier.Status = status;
            AssertEx.True(!(await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentRead, "SUP1")).IsAllowed);
        }
        supplier.Status = "Active";
        supplier.ExpiryDate = DateTime.Now.AddDays(-1);
        AssertEx.True(!(await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentRead, "SUP1")).IsAllowed);
        supplier.ExpiryDate = null;
        AssertEx.True(!(await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentRead, "SUP1")).IsAllowed);
        supplier.ExpiryDate = DateTime.Now.AddDays(5);
        options.SupplierPermissions = [GroupBPermissions.FulfillmentRead];
        AssertEx.True(!(await service.AuthorizeAsync("SUP1", GroupBPermissions.FulfillmentWrite, "SUP1")).IsAllowed);
    }

    private static AuthorizationFilterContext Context(Type controller, string action, string? key = null, string? id = null)
    {
        var http = new DefaultHttpContext { Session = new AuthorizationSession() };
        if (key != null && id != null) http.Session.SetString(key, id);
        var method = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .First(item => item.Name == action);
        var descriptor = new ControllerActionDescriptor { MethodInfo = method, ControllerTypeInfo = controller.GetTypeInfo() };
        return new(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());
    }

    private static async Task AdminFilterAsync()
    {
        var service = Service(User(), Role(), Grants(GroupBPermissions.OrdersRead));
        var filter = new GroupBAdminSessionAuthorizationFilter(service, NullLogger<GroupBAdminSessionAuthorizationFilter>.Instance);
        var oldSession = Context(typeof(OrderController), "Index", "AdminName", "old-admin");
        await filter.OnAuthorizationAsync(oldSession);
        AssertEx.True(oldSession.Result is RedirectToActionResult);
        var read = Context(typeof(OrderController), "Index", "AdminId", "ADMIN1");
        await filter.OnAuthorizationAsync(read);
        AssertEx.True(read.Result == null);
        foreach (var action in new[] { "Create", "Transition", "Cancel" })
        {
            var write = Context(typeof(OrderController), action, "AdminId", "ADMIN1");
            await filter.OnAuthorizationAsync(write);
            AssertEx.Equal(403, ((StatusCodeResult)write.Result!).StatusCode);
        }
    }

    private static async Task SupplierFilterAsync()
    {
        var service = FinancialProxy.Create<IGroupCAuthorizationService>((_, args) =>
        {
            AssertEx.Equal("SUP1", (string)args![0]!);
            AssertEx.Equal("SUP1", (string)args[2]!);
            return Task.FromResult(new GroupCAuthorizationResult
            {
                SubjectId = "SUP1", IsAllowed = (string)args[1]! == GroupBPermissions.FulfillmentRead
            });
        });
        var filter = new GroupBSupplierSessionAuthorizationFilter(service, NullLogger<GroupBSupplierSessionAuthorizationFilter>.Instance);
        var anonymous = Context(typeof(SupplierFulfillmentController), "Index");
        await filter.OnAuthorizationAsync(anonymous);
        AssertEx.True(anonymous.Result is RedirectToActionResult);
        foreach (var action in new[] { "Index", "Detail", "Ship", "AddTrackingEvent" })
        {
            var context = Context(typeof(SupplierFulfillmentController), action, "SupplierId", "SUP1");
            context.RouteData.Values["SupplierId"] = "ATTACKER-SUPPLIER";
            await filter.OnAuthorizationAsync(context);
            if (action is "Index" or "Detail") AssertEx.True(context.Result == null);
            else AssertEx.Equal(403, ((StatusCodeResult)context.Result!).StatusCode);
        }
    }

    private static async Task FailureClosedAsync()
    {
        foreach (var failure in new[] { "exception", "identity" })
        {
            var service = FinancialProxy.Create<IGroupCAuthorizationService>((_, _) => failure == "exception"
                ? throw new InvalidOperationException("模拟授权库故障")
                : Task.FromResult(new GroupCAuthorizationResult { IsAllowed = true, SubjectId = "OTHER" }));
            var filter = new GroupBAdminSessionAuthorizationFilter(service, NullLogger<GroupBAdminSessionAuthorizationFilter>.Instance);
            var context = Context(typeof(OrderController), "Index", "AdminId", "ADMIN1");
            await filter.OnAuthorizationAsync(context);
            AssertEx.Equal(failure == "exception" ? 503 : 403, ((StatusCodeResult)context.Result!).StatusCode);
        }
        var unknown = Context(typeof(UnannotatedAction), "Index", "AdminId", "ADMIN1");
        await new GroupBAdminSessionAuthorizationFilter(FinancialProxy.Create<IGroupCAuthorizationService>(),
            NullLogger<GroupBAdminSessionAuthorizationFilter>.Instance).OnAuthorizationAsync(unknown);
        AssertEx.Equal(403, ((StatusCodeResult)unknown.Result!).StatusCode);
    }

    private static Task AllActionsDeclarePermissionsAsync()
    {
        foreach (var controller in new[] { typeof(OrderController), typeof(SupplierFulfillmentController) })
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var permission = method.GetCustomAttribute<GroupBPermissionAttribute>();
            AssertEx.True(permission != null);
            if (method.GetCustomAttribute<HttpPostAttribute>() != null)
                AssertEx.True(permission!.Code is GroupBPermissions.OrdersManage or GroupBPermissions.FulfillmentWrite);
        }
        return Task.CompletedTask;
    }

    private static async Task AdminLoginAsync()
    {
        var user = User();
        user.Username = "test-admin";
        user.PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("test-password")));
        var service = new SystemAdminService(new FinancialUnitOfWork(), Admins(user, Role()),
            FinancialProxy.Create<IPromoterRepository>(), FinancialProxy.Create<ITableLogService>());
        foreach (var status in new[] { "Disabled", "Disable", "Locked", "Pending", "" })
        {
            user.Status = status;
            AssertEx.True(!service.LoginAdmin("test-admin", "test-password").IsSuccess);
        }
        user.Status = "Enabled";
        // 此场景只运行管理员分支，不调用团长或供应商服务。
        var controller = new AccountController(null!, service, null!)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new AuthorizationSession() } } };
        var result = await controller.Login("test-admin", "test-password", "管理员");
        AssertEx.True(result is RedirectToActionResult);
        AssertEx.Equal("ADMIN1", controller.HttpContext.Session.GetString("AdminId"));
    }

    private sealed class UnannotatedAction { public void Index() { } }
}

internal sealed class AuthorizationOptionsSnapshot(GroupCAuthorizationOptions value) : IOptionsSnapshot<GroupCAuthorizationOptions>
{
    public GroupCAuthorizationOptions Value => value;
    public GroupCAuthorizationOptions Get(string? name) => value;
}

internal sealed class AuthorizationSession : ISession
{
    private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);
    public string Id => "test-session";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _values.Keys;
    public void Clear() => _values.Clear();
    public void Remove(string key) => _values.Remove(key);
    public void Set(string key, byte[] value) => _values[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
