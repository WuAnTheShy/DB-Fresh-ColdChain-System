using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Oracle.ManagedDataAccess.Client;

// Usage: dotnet run --project tests/SeedConsumers [outputSqlPath]
// If outputSqlPath omitted, the generated seed SQL is written alongside this project.

var outputSqlPath = args.Length > 0 ? args[0] : Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "migrations", "seed_test_consumers.sql");

// Look for the repo-root appsettings.json to reuse the configured connection string.
var repoRoot = FindRepoRoot();
string connectionString;
if (repoRoot != null)
{
    var settingsPath = Path.Combine(repoRoot, "appsettings.json");
    var json = JsonDocument.Parse(File.ReadAllText(settingsPath));
    connectionString = json.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("OracleConnection")
        .GetString() ?? throw new InvalidOperationException("缺少 OracleConnection 连接字符串");
    Console.WriteLine("Connection string source: " + settingsPath);
}
else
{
    connectionString = "User Id=COLDCHAIN;Password=Oracle123;Data Source=111.231.80.220:1521;";
    Console.WriteLine("Connection string source: fallback default");
}

// Shared password for all ten test accounts (>=8 chars to satisfy login validation).
const string testPassword = "TestUser2026!";
const string baseLevelId = "00000000000000000000000000000001"; // 普通会员 (MinSpent = 0)
string[] avatars = ["cat", "rabbit", "panda", "fox", "carrot", "broccoli", "tomato", "corn"];

string[] realNames =
[
    "李思远", "王雨桐", "张雅静", "陈浩然", "刘欣怡",
    "赵梦琪", "孙立杰", "周子涵", "吴佳琪", "郑晓东"
];
var accounts = new List<(string Name, string Phone, string Email, string Avatar)>();
for (var i = 1; i <= 10; i++)
{
    accounts.Add((
        realNames[i - 1],
        $"139000010{i:00}",
        $"test-consumer{i:00}@example.com",
        avatars[(i - 1) % avatars.Length]));
}

// --rename mode: assign realistic display names to the 10 test consumers (跟团记录脱敏展示前缀来源)。
if (args.Any(a => a == "--rename"))
{
    await using var rn = new OracleConnection(connectionString);
    await rn.OpenAsync();
    for (var i = 0; i < accounts.Count; i++)
    {
        using var upd = rn.CreateCommand();
        upd.CommandText = "UPDATE Crm_Customers SET CustomerName = :Name, UpdatedAt = SYSDATE WHERE Phone = :Phone";
        upd.Parameters.Add(new OracleParameter("Name", realNames[i]));
        upd.Parameters.Add(new OracleParameter("Phone", accounts[i].Phone));
        var affected = await upd.ExecuteNonQueryAsync();
        Console.WriteLine($"RENAME {accounts[i].Phone} -> {realNames[i]} ({(affected == 1 ? "ok" : "not found")})");
    }
    Console.WriteLine("done renaming test consumers.");
    return;
}

// --seed-purchases mode: create completed purchase records for the 10 test consumers
// buying the given product from the given promoter, so the 跟团记录 has data to show.
// Usage: --seed-purchases <promoterId> <productId> <supplierId> <unitPrice>
if (args.Any(a => a == "--seed-purchases"))
{
    var idx = Array.IndexOf(args, "--seed-purchases");
    var promoterId = args.ElementAtOrDefault(idx + 1) ?? string.Empty;
    var productId = args.ElementAtOrDefault(idx + 2) ?? string.Empty;
    var supplierId = args.ElementAtOrDefault(idx + 3) ?? string.Empty;
    var unitPrice = args.ElementAtOrDefault(idx + 4) is { } p && decimal.TryParse(p, out var up) ? up : 0m;
    if (string.IsNullOrWhiteSpace(promoterId) || string.IsNullOrWhiteSpace(productId) || unitPrice <= 0)
    {
        Console.WriteLine("usage: --seed-purchases <promoterId> <productId> <supplierId> <unitPrice>");
        return;
    }

    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var orders = new[] { 1, 1, 2, 1, 3, 2, 1, 4, 2, 1 }; // per-consumer purchase quantity
    await using var sp = new OracleConnection(connectionString);
    await sp.OpenAsync();
    for (var i = 0; i < accounts.Count; i++)
    {
        var acc = accounts[i];
        var quantity = orders[i];
        var total = unitPrice * quantity;

        // 1) resolve customer id by phone
        string customerId;
        using (var find = sp.CreateCommand())
        {
            find.CommandText = "SELECT CustomerId FROM Crm_Customers WHERE Phone = :Phone";
            find.Parameters.Add(new OracleParameter("Phone", acc.Phone));
            customerId = (string?)await find.ExecuteScalarAsync() ?? throw new InvalidOperationException($"找不到消费者 {acc.Phone}");
        }

        // 2) ensure at least one address (Biz_Orders.AddressId is NOT NULL with FK)
        string addressId;
        using (var addrCheck = sp.CreateCommand())
        {
            addrCheck.CommandText = "SELECT AddressId FROM Crm_UserAddresses WHERE CustomerId = :CustomerId AND ROWNUM = 1";
            addrCheck.Parameters.Add(new OracleParameter("CustomerId", customerId));
            addressId = (string?)await addrCheck.ExecuteScalarAsync() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(addressId))
        {
            addressId = Guid.NewGuid().ToString("N");
            using var addr = sp.CreateCommand();
            addr.CommandText = @"INSERT INTO Crm_UserAddresses (
                    AddressId, CustomerId, ReceiverName, Phone, Province, City, District,
                    DetailAddress, IsDefault, CreatedAt)
                VALUES (:AddressId, :CustomerId, :ReceiverName, :Phone, '浙江省', '杭州市', '西湖区',
                    '测试收件地址' || :Seq, 1, SYSDATE)";
            addr.Parameters.Add(new OracleParameter("AddressId", addressId));
            addr.Parameters.Add(new OracleParameter("CustomerId", customerId));
            addr.Parameters.Add(new OracleParameter("ReceiverName", "测试收件人" + (i + 1)));
            addr.Parameters.Add(new OracleParameter("Phone", acc.Phone));
            addr.Parameters.Add(new OracleParameter("Seq", i + 1));
            await addr.ExecuteNonQueryAsync();
        }

        // 3) create a completed order with one detail line for the product
        var orderId = Guid.NewGuid().ToString("N");
        var orderDetailId = Guid.NewGuid().ToString("N");
        var orderNo = $"TEST-GRP-{used.Count + 1:000}";
        if (!used.Add(orderNo)) orderNo = $"TEST-GRP-{Guid.NewGuid():N}"[..18];

        using var orderCmd = sp.CreateCommand();
        orderCmd.CommandText = @"INSERT INTO Biz_Orders (
                OrderId, OrderNo, CustomerId, CheckoutBatchId, PromoterId, AddressId,
                ReceiverName, ReceiverPhone, ShippingAddress, TotalAmount, DiscountAmount,
                FreightAmount, FinalAmount, PointsEarned, PointsUsed, PointsDiscountAmount,
                OrderStatus, PaymentExpiresAt, CreatedAt)
            VALUES (
                :OrderId, :OrderNo, :CustomerId, NULL, :PromoterId, :AddressId,
                :ReceiverName, :ReceiverPhone, :ShippingAddress, :TotalAmount, 0,
                0, :FinalAmount, 0, 0, 0,
                'COMPLETED', NULL, :CreatedAt)";
        orderCmd.Parameters.Add(new OracleParameter("OrderId", orderId));
        orderCmd.Parameters.Add(new OracleParameter("OrderNo", orderNo));
        orderCmd.Parameters.Add(new OracleParameter("CustomerId", customerId));
        orderCmd.Parameters.Add(new OracleParameter("PromoterId", promoterId));
        orderCmd.Parameters.Add(new OracleParameter("AddressId", addressId));
        orderCmd.Parameters.Add(new OracleParameter("ReceiverName", "测试收件人" + (i + 1)));
        orderCmd.Parameters.Add(new OracleParameter("ReceiverPhone", acc.Phone));
        orderCmd.Parameters.Add(new OracleParameter("ShippingAddress", "浙江省 杭州市 西湖区 测试收件地址" + (i + 1)));
        orderCmd.Parameters.Add(new OracleParameter("TotalAmount", total));
        orderCmd.Parameters.Add(new OracleParameter("FinalAmount", total));
        orderCmd.Parameters.Add(new OracleParameter("CreatedAt", DateTime.Now.AddDays(-(i % 5 + 1))));
        await orderCmd.ExecuteNonQueryAsync();

        using var detailCmd = sp.CreateCommand();
        detailCmd.CommandText = @"INSERT INTO Biz_OrderDetails (
                OrderDetailId, OrderId, ProductId, ProductName, Quantity,
                UnitPrice, SubTotal, SupplierId, ReceiptStatus)
            VALUES (
                :OrderDetailId, :OrderId, :ProductId, :ProductName, :Quantity,
                :UnitPrice, :SubTotal, :SupplierId, 'RECEIVED')";
        detailCmd.Parameters.Add(new OracleParameter("OrderDetailId", orderDetailId));
        detailCmd.Parameters.Add(new OracleParameter("OrderId", orderId));
        detailCmd.Parameters.Add(new OracleParameter("ProductId", productId));
        detailCmd.Parameters.Add(new OracleParameter("ProductName", "精选羊肉卷 500g"));
        detailCmd.Parameters.Add(new OracleParameter("Quantity", quantity));
        detailCmd.Parameters.Add(new OracleParameter("UnitPrice", unitPrice));
        detailCmd.Parameters.Add(new OracleParameter("SubTotal", total));
        detailCmd.Parameters.Add(new OracleParameter("SupplierId", supplierId));
        await detailCmd.ExecuteNonQueryAsync();

        Console.WriteLine($"SEEDED PURCHASE: {acc.Phone} {acc.Name} qty={quantity} total={total} order={orderNo}");
    }

    Console.WriteLine("done seeding test purchases.");
    return;
}

var hasher = new PasswordHasher<CrmCustomer>();

// --list mode: print existing customers and exit.
if (args.Any(a => a == "--list"))
{
    using (var listConn = new OracleConnection(connectionString))
    {
        await listConn.OpenAsync();
        using var listCmd = listConn.CreateCommand();
        listCmd.CommandText = @"SELECT CustomerId, CustomerName, Phone, Email, Avatar, MemberLevelId,
                                       TotalSpent, Points, CreatedAt
                                FROM Crm_Customers ORDER BY CreatedAt, CustomerId";
        using var reader = await listCmd.ExecuteReaderAsync();
        var headerDone = false;
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"ID={reader["CustomerId"]} | NAME={reader["CustomerName"]} | PHONE={reader["Phone"]} | EMAIL={reader["Email"]} | AVATAR={reader["Avatar"]} | LVL={reader["MemberLevelId"]} | SPENT={reader["TotalSpent"]} | PTS={reader["Points"]} | CREATED={reader["CreatedAt"]}");
            headerDone = true;
        }
        if (!headerDone) Console.WriteLine("(no customers)");
    }
    return;
}

// --verify mode: confirm the stored hashes validate against the shared test password.
if (args.Any(a => a == "--verify"))
{
    var verifyConn = new OracleConnection(connectionString);
    await verifyConn.OpenAsync();
    int ok = 0;
    int bad = 0;
    foreach (var acc in accounts)
    {
        using var q = verifyConn.CreateCommand();
        q.CommandText = "SELECT PasswordHash FROM Crm_Customers WHERE Phone = :Phone";
        q.Parameters.Add(new OracleParameter("Phone", acc.Phone));
        var hash = (string?)await q.ExecuteScalarAsync();
        if (hash == null)
        {
            Console.WriteLine($"MISSING: {acc.Phone} {acc.Name}");
            bad++;
        }
        else
        {
            var result = hasher.VerifyHashedPassword(new CrmCustomer { CustomerName = acc.Name }, hash, testPassword);
            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                Console.WriteLine($"VERIFY OK: {acc.Phone} {acc.Name}");
                ok++;
            }
            else
            {
                Console.WriteLine($"VERIFY FAILED: {acc.Phone} {acc.Name} ({result})");
                bad++;
            }
        }
    }
    Console.WriteLine($"Verified: {ok}; Failed/Missing: {bad}");
    return;
}

var sb = new StringBuilder();
sb.AppendLine("-- ============================================================");
sb.AppendLine("-- 测试消费者账号种子脚本 (10 个)");
sb.AppendLine($"-- 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
sb.AppendLine($"-- 共享测试密码: {testPassword}");
sb.AppendLine("-- ============================================================");
sb.AppendLine();

using var conn = new OracleConnection(connectionString);
conn.Open();

int inserted = 0, skipped = 0, failed = 0;
foreach (var acc in accounts)
{
    var customerId = Guid.NewGuid().ToString("N"); // VARCHAR2(36)

    using (var check = conn.CreateCommand())
    {
        check.CommandText = "SELECT COUNT(1) FROM Crm_Customers WHERE Phone = :Phone";
        check.Parameters.Add(new OracleParameter("Phone", acc.Phone));
        var exists = Convert.ToInt32(await check.ExecuteScalarAsync());
        if (exists > 0)
        {
            Console.WriteLine($"SKIP (phone already exists): {acc.Phone} {acc.Name}");
            skipped++;
            continue;
        }
    }

    var hash = hasher.HashPassword(new CrmCustomer { CustomerName = acc.Name }, testPassword);

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            INSERT INTO Crm_Customers (
                CustomerId, CustomerName, Phone, Email, Avatar, PasswordHash,
                OpenId, PromoterId, MemberLevelId, TotalSpent, Points, GrowthValue,
                BindExpireTime, CreatedAt)
            VALUES (
                :CustomerId, :CustomerName, :Phone, :Email, :Avatar, :PasswordHash,
                NULL, NULL, :MemberLevelId, 0, 0, 0, NULL, SYSDATE)";

        cmd.Parameters.Add(new OracleParameter("CustomerId", customerId));
        cmd.Parameters.Add(new OracleParameter("CustomerName", acc.Name));
        cmd.Parameters.Add(new OracleParameter("Phone", acc.Phone));
        cmd.Parameters.Add(new OracleParameter("Email", acc.Email));
        cmd.Parameters.Add(new OracleParameter("Avatar", acc.Avatar));
        cmd.Parameters.Add(new OracleParameter("PasswordHash", hash));
        cmd.Parameters.Add(new OracleParameter("MemberLevelId", baseLevelId));

        try { await cmd.ExecuteNonQueryAsync(); }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {acc.Phone} {acc.Name} -> {ex.Message}");
            failed++;
            continue;
        }
    }

    inserted++;
    Console.WriteLine($"INSERT OK: {acc.Phone} {acc.Name} -> {customerId}");

    sb.AppendLine("INSERT INTO Crm_Customers (");
    sb.AppendLine("    CustomerId, CustomerName, Phone, Email, Avatar, PasswordHash,");
    sb.AppendLine("    OpenId, PromoterId, MemberLevelId, TotalSpent, Points, GrowthValue,");
    sb.AppendLine("    BindExpireTime, CreatedAt)");
    sb.AppendLine("VALUES (");
    sb.AppendLine($"    '{customerId}', '{acc.Name}', '{acc.Phone}', '{acc.Email}', '{acc.Avatar}',");
    sb.AppendLine($"    '{hash}',");
    sb.AppendLine($"    NULL, NULL, '{baseLevelId}', 0, 0, 0, NULL, SYSDATE);");
    sb.AppendLine();
}

conn.Close();

var fullPath = Path.GetFullPath(outputSqlPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

Console.WriteLine();
Console.WriteLine($"Inserted: {inserted}, Skipped: {skipped}, Failed: {failed}");
Console.WriteLine($"SQL artifact written to: {fullPath}");

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "FreshColdChain.csproj")))
            return dir.FullName;
    return null;
}

internal sealed class CrmCustomer
{
    public string CustomerName { get; set; } = string.Empty;
}
