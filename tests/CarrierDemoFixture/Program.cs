using System.Text.Json;
using Dapper;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using FreshColdChain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

// 仅用于物流演示：创建专用零金额测试订单，不伪造真实支付、不扣真实库存，不修改已有数据。
if (!args.Contains("--seed") && !args.Contains("--verify"))
{
    Console.WriteLine("在仓库根目录运行：dotnet run --project tests/CarrierDemoFixture -- --seed 或 --verify");
    return;
}
var repo = Directory.GetCurrentDirectory();
var config = new ConfigurationBuilder().SetBasePath(repo).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
const string customerId = "CARRIER-DEMO-CUSTOMER";
const string supplierId = "SUP-CARRIER-DEMO";
const string phone = "19900000001";
var customers = new CustomerRepository(config);
var orders = new OrderRepository(config);
await using var connection = new OracleConnection(config.GetConnectionString("OracleConnection"));
await connection.OpenAsync();
using var unitOfWork = new UnitOfWork(new OracleDbConnectionFactory(config));
var repository = new GroupALogisticsRepository(unitOfWork);
var provider = new OracleGroupALogisticsExtensionProvider(repository, Options.Create(new GroupALogisticsOptions()));

if (args.Contains("--seed"))
{
    using var transaction = connection.BeginTransaction();
    unitOfWork.AttachExternalTransaction(transaction);
    var suppliers = new BaseRepository<InvSupplier>(unitOfWork);
    var products = new BaseRepository<InvProduct>(unitOfWork);
    var supplier = await suppliers.GetByIdAsync(supplierId);
    if (supplier == null)
        await suppliers.AddAsync(new() { SupplierID = supplierId, SupplierName = "物流演示专用供应商", Status = "Disabled" });
    else if (supplier.SupplierName != "物流演示专用供应商")
        throw new InvalidOperationException("演示供应商标识冲突，拒绝覆盖");
    const string productId = "CARRIER-DEMO-PRODUCT";
    var product = await products.GetByIdAsync(productId);
    if (product == null)
        await products.AddAsync(new() { ProductID = productId, SupplierID = supplierId,
            ProductName = "物流演示包裹（非交易商品）", Status = "INACTIVE", Unit = "件", StorageReq = "CHILLED" });
    else if (product.SupplierID != supplierId || product.ProductName != "物流演示包裹（非交易商品）")
        throw new InvalidOperationException("演示商品标识冲突，拒绝覆盖");
    var customer = await customers.GetByIdAsync(customerId, transaction);
    string? password = null;
    if (customer == null)
    {
        if (await customers.PhoneExistsAsync(phone, transaction: transaction))
            throw new InvalidOperationException("测试手机号已被其他账号占用，未修改任何数据");
        password = "Demo!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
        customer = new() { CustomerId = customerId, Phone = phone, CustomerName = "物流模拟演示账号", Avatar = "panda" };
        customer.PasswordHash = new PasswordHasher<CrmCustomer>().HashPassword(customer, password);
        await customers.CreateCustomerAsync(customer, transaction);
        await customers.CreateAddressAsync(new()
        {
            AddressId = "CARRIER-DEMO-ADDRESS", CustomerId = customerId, ReceiverName = "物流演示收件人", Phone = phone,
            Province = "浙江省", City = "杭州市", District = "西湖区", DetailAddress = "仅供物流演示的测试地址", IsDefault = 1
        }, transaction);
    }
    else if (customer.CustomerName != "物流模拟演示账号" || customer.Phone != phone)
        throw new InvalidOperationException("测试标识发生冲突，拒绝覆盖已有账号");

    var created = 0;
    for (var index = 1; index <= 3; index++)
    {
        var orderId = $"ORDER-DEMO-{index:00}";
        var existing = await orders.GetByIdAsync(orderId, transaction);
        if (existing != null)
        {
            if (existing.CustomerId != customerId) throw new InvalidOperationException("测试订单编号被占用，拒绝覆盖");
            continue;
        }
        await orders.CreateOrderAsync(new()
        {
            OrderId = orderId, OrderNo = orderId, CustomerId = customerId, AddressId = "CARRIER-DEMO-ADDRESS",
            ReceiverName = "物流演示收件人", ReceiverPhone = phone, ShippingAddress = "仅供物流演示的测试地址",
            OrderStatus = OrderStatusCodes.Shipped, TotalAmount = 0, FinalAmount = 0
        }, transaction);
        await orders.InsertDetailsAsync([new()
        {
            OrderDetailId = $"CARRIER-DEMO-ITEM-{index:00}", OrderId = orderId, SupplierId = supplierId,
            ProductId = "CARRIER-DEMO-PRODUCT", ProductName = "物流演示包裹（非交易商品）", Quantity = 1
        }], transaction);
        var shippedAt = DateTime.Now.AddMinutes(-10);
        shippedAt = new DateTime(shippedAt.Ticks - shippedAt.Ticks % TimeSpan.TicksPerSecond);
        var deliveryId = $"CARRIER-DEMO-DEL-{index:00}";
        await new LogExpressDeliveryRepository(unitOfWork).AddAsync(new()
        {
            DeliveryID = deliveryId, OrderID = orderId, SupplierID = supplierId, ShippedAt = shippedAt,
            LogisticsCompany = "演示物流商", TrackingNo = $"DEMO-{index:000}", PackageTemp = "CHILLED"
        });
        await provider.RegisterShipmentAsync(new()
        {
            DeliveryId = deliveryId, OrderId = orderId, SupplierId = supplierId,
            Command = new() { SupplierId = supplierId, CarrierCode = "DEMO", CarrierName = "演示物流商",
                TrackingNo = $"DEMO-{index:000}", EstimatedArrivalAt = DateTime.Now.AddDays(1), Remark = "物流专用演示数据，不涉及真实库存或付款" }
        }, transaction);
        if (index != 2)
            await provider.AppendTrackingEventAsync(new()
            {
                EventId = $"CARRIER-DEMO-TRANSIT-{index:00}", OrderId = orderId, SupplierId = supplierId,
                StatusCode = LogisticsStatusCodes.InTransit, OccurredAt = shippedAt.AddMinutes(1),
                Location = "杭州中转中心", Description = "演示包裹已到达冷链中转中心", TemperatureCelsius = 4
            }, transaction);
        if (index == 3)
            await provider.AppendTrackingEventAsync(new()
            {
                EventId = "CARRIER-DEMO-OUT-03", OrderId = orderId, SupplierId = supplierId,
                StatusCode = LogisticsStatusCodes.OutForDelivery, OccurredAt = shippedAt.AddMinutes(2),
                Location = "杭州配送站", Description = "演示包裹开始派送", TemperatureCelsius = 4
            }, transaction);
        created++;
    }
    transaction.Commit();
    if (password != null)
    {
        var directory = Path.Combine(repo, "tmp", "carrier-demo");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "consumer-access.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { phone, password, customerId }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"演示登录信息仅保存于本机忽略文件：{path}");
    }
    Console.WriteLine($"新增 {created} 张测试运单；已有演示记录不覆盖，未修改其他订单、库存或支付记录。");
}

if (args.Contains("--verify"))
{
    using var transaction = connection.BeginTransaction();
    unitOfWork.AttachExternalTransaction(transaction);
    var before = await repository.GetEventsAsync("CARRIER-DEMO-DEL-02", transaction);
    var command = new LogisticsTrackingEventCommand
    {
        EventId = "CARRIER-VERIFY-" + Guid.NewGuid().ToString("N")[..20], OrderId = "ORDER-DEMO-02", SupplierId = supplierId,
        StatusCode = LogisticsStatusCodes.InTransit, OccurredAt = DateTime.Now.AddSeconds(-1),
        Description = "可回滚的 Oracle 精度与事件重试验收", Location = "演示测试站", TemperatureCelsius = 4
    };
    await provider.AppendTrackingEventAsync(command, transaction);
    await provider.AppendTrackingEventAsync(command, transaction);
    var within = await repository.GetEventsAsync("CARRIER-DEMO-DEL-02", transaction);
    if (within.Count != before.Count + 1 || within.Last().OccurredAt.Ticks != command.OccurredAt.Ticks)
        throw new InvalidOperationException("真实 Oracle 事件幂等或时间精度验证失败");
    transaction.Rollback();
    using var independent = new UnitOfWork(new OracleDbConnectionFactory(config));
    var after = await new GroupALogisticsRepository(independent).GetEventsAsync("CARRIER-DEMO-DEL-02");
    if (after.Count != before.Count) throw new InvalidOperationException("真实 Oracle 事务回滚验证失败");
    var search = await new GroupACarrierRepository(independent).SearchAsync([supplierId], "DEMO-002", CancellationToken.None);
    if (search.Count != 1) throw new InvalidOperationException("真实 Oracle 运单查询验证失败");
    var denied = await new GroupACarrierRepository(independent).FindAsync("CARRIER-DEMO-DEL-02", ["NOT-AUTHORIZED"], null, CancellationToken.None);
    if (denied != null) throw new InvalidOperationException("供应商隔离验证失败");
    Console.WriteLine("PASS Oracle 已提交轨迹读取、TIMESTAMP(7) 精度、同事件重试、事务回滚、运单查询和供应商隔离。");
}
