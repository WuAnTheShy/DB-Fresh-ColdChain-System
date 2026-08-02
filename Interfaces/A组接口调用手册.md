# A 组接口调用手册

> 适用读者：B 组、C 组开发人员  
> 说明：本文档列出 A 组对外暴露的所有接口、调用方式和注意事项  
> 更新日期：2026-08-02

---

## 〇、核心规则

1. **事务由调用方控制**。B 组创建 `IDbTransaction`，传给 A 组。A 组只负责执行，不提交不回滚。
2. **所有 ID 都是 `string` 类型（VARCHAR2(36) UUID 格式）**。例如 `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`。
3. **A 组不信任客户端数据**。商品名称、单价、供应商 ID 全部由 A 组查数据库返回。
4. **调用 A 组的接口前，在 `Program.cs` 注册 A 组的适配器**（见下文）。

---

## 一、注册 A 组的服务（各组的 Program.cs 加三行）

```csharp
builder.Services.AddScoped<IInventoryService, InventoryServiceAdapter>();
builder.Services.AddScoped<ILogisticsService, LogisticsServiceAdapter>();
builder.Services.AddScoped<ICommissionService, DummyCommissionService>();  // C 组完成后替换
```

---

## 二、库存接口 — IInventoryService

### 接口定义

```csharp
public interface IInventoryService
{
    // 锁定库存 + 返回商品快照
    Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
        IReadOnlyList<InventoryReservationItem> items,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    // 释放已锁定的库存
    Task ReleaseAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
```

### 2.1 ReserveAsync — 锁定库存

**什么时候调用**：创建订单时，在同一个事务里先锁库存再建订单。

**输入**：

```csharp
var items = new List<InventoryReservationItem>
{
    new() { ProductId = "uuid-产品1", Quantity = 5 },
    new() { ProductId = "uuid-产品2", Quantity = 3 },
};
```

**返回**：

```csharp
// IReadOnlyList<InventoryProductSnapshot>
[
    {
        ProductId   = "uuid-产品1",
        ProductName = "有机西兰花",      // ← A 组查的，你不需要自己填
        SupplierId  = "uuid-供应商A",    // ← A 组查的
        UnitPrice   = 12.50m            // ← A 组查的，你不需要信任客户端价格
    },
    {
        ProductId   = "uuid-产品2",
        ProductName = "三文鱼",
        SupplierId  = "uuid-供应商B",
        UnitPrice   = 80.00m
    }
]
```

**调用示例（B 组下单时）**：

```csharp
public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request)
{
    return await _transactionManager.ExecuteAsync(async transaction =>
    {
        // 1. 调 A 组锁库存
        var items = request.Items.Select(i => new InventoryReservationItem
        {
            ProductId = i.ProductId,
            Quantity  = i.Quantity
        }).ToList();

        var snapshots = await _inventoryService.ReserveAsync(items, transaction);
        //                ↑ 传入 B 组的事务，库存锁和后续订单在同一事务

        // 2. 用 A 组返回的快照计算商品金额（不要用客户端的价格！）
        decimal goodsAmount = 0;
        var details = new List<BizOrderDetail>();
        foreach (var item in request.Items)
        {
            var snapshot = snapshots.First(s => s.ProductId == item.ProductId);
            var subTotal = snapshot.UnitPrice * item.Quantity;
            goodsAmount += subTotal;
            details.Add(new BizOrderDetail
            {
                ProductId   = snapshot.ProductId,
                ProductName = snapshot.ProductName,   // ← 用 A 组的
                SupplierId  = snapshot.SupplierId,    // ← 用 A 组的
                UnitPrice   = snapshot.UnitPrice,     // ← 用 A 组的
                Quantity    = item.Quantity,
                SubTotal    = subTotal
            });
        }

        // 3. 创建订单（你自己的表）
        var order = new BizOrder { TotalAmount = goodsAmount, ... };
        await _orderRepo.CreateOrderAsync(order, transaction);

        // 4. 整个事务提交 → A 组库存锁 + B 组订单同时生效
        return new CreateOrderResult { ... };
    });
}
```

**失败时**：抛 `InvalidOperationException`（库存不足/商品不存在），B 组的事务自动回滚。

### 2.2 ReleaseAsync — 释放库存

**什么时候调用**：取消未发货订单时，释放之前锁定的库存。

**调用示例**：

```csharp
public async Task CancelOrderAsync(int orderId)
{
    await _transactionManager.ExecuteAsync(async transaction =>
    {
        var order = await _orderRepo.GetByIdForUpdateAsync(orderId, transaction);
        var details = await _orderRepo.GetDetailsAsync(orderId, transaction);

        // 调 A 组释放库存
        await _inventoryService.ReleaseAsync(
            new FulfillmentOrderRequest
            {
                OrderId = order.OrderId,
                Items = details.Select(d => new FulfillmentOrderItem
                {
                    ProductId = d.ProductId,
                    Quantity  = d.Quantity
                }).ToList()
            },
            transaction);

        // 取消订单
        await _orderRepo.TryUpdateStatusAsync(orderId, ..., OrderStatus.Cancelled, transaction);
    });
}
```

---

## 三、物流接口 — ILogisticsService

### 接口定义

```csharp
public interface ILogisticsService
{
    // 计算冷链运费
    Task<decimal> CalculateFreightAsync(
        FreightCalculationRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    // FEFO 批次扣减 + 创建发货单 + 批次溯源
    Task CreateShipmentAsync(
        FulfillmentOrderRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    // 查询供应商履约状态（只读，可以不传事务）
    Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
        string orderId,
        IReadOnlyList<string> supplierIds,
        CancellationToken cancellationToken = default);
}
```

### 3.1 CalculateFreightAsync — 计算冷链运费

**什么时候调用**：创建订单时，算运费加到订单金额里。

**调用示例**：

```csharp
var freight = await _logisticsService.CalculateFreightAsync(
    new FreightCalculationRequest
    {
        CustomerId  = customerId,
        Province    = address.Province,     // "广东省"
        City        = address.City,         // "深圳市"
        District    = address.District,     // "南山区"
        GoodsAmount = goodsAmount,          // 商品金额（用于判断包邮）
        Items       = snapshots.Select(s => new FulfillmentOrderItem
        {
            ProductId   = s.ProductId,
            ProductName = s.ProductName,
            SupplierId  = s.SupplierId,
            Quantity    = ...,
            UnitPrice   = s.UnitPrice
        }).ToList()
    },
    transaction);

// freight = 运费金额（decimal），例如 15.00m
```

**计费规则**：
- 按商品温区（冷藏 CHILLED / 冷冻 FROZEN / 常温 AMBIENT）匹配运费模板
- 按省→市→区优先级精确匹配
- 首重 + 续重阶梯 + 冷链包装费
- 满足包邮阈值时返回 0

**前置条件**：A 组需要先在 `/ColdChainLogistics/CreateTemplate` 页面配置运费模板。

### 3.2 CreateShipmentAsync — 创建发货

**什么时候调用**：订单状态从"已支付"流转到"已发货"时。

**调用示例**：

```csharp
await _logisticsService.CreateShipmentAsync(
    new FulfillmentOrderRequest
    {
        OrderId        = order.OrderId,
        OrderNo        = order.OrderNo,
        ReceiverName   = order.ReceiverName,
        ReceiverPhone  = order.ReceiverPhone,
        ShippingAddress = order.ShippingAddress,
        Items = details.Select(d => new FulfillmentOrderItem
        {
            ProductId   = d.ProductId,
            ProductName = d.ProductName,
            SupplierId  = d.SupplierId,
            Quantity    = d.Quantity
        }).ToList()
    },
    transaction);
```

**A 组做了什么**：
1. 按 FEFO（最早过期优先）扣减批次库存
2. 创建 `Log_ExpressDeliveries` 发货单
3. 创建 `LogFulfillmentBatchItems` 批次溯源记录
4. 全部在 B 组的事务内完成

**失败时**：批次库存不足抛 `InvalidOperationException`，整个事务回滚。

### 3.3 GetSupplierStatusesAsync — 查询履约状态

**什么时候调用**：查看订单详情时，展示每个供应商的发货状态。

**调用示例**：

```csharp
// 只读查询，可以不传 transaction
var statuses = await _logisticsService.GetSupplierStatusesAsync(
    orderId: "uuid-订单",
    supplierIds: new List<string> { "uuid-供应商A", "uuid-供应商B" });

// 返回：
// [
//     { SupplierId = "uuid-供应商A", StatusName = "SHIPPED", TrackingNo = "CC..." },
//     { SupplierId = "uuid-供应商B", StatusName = "未发货",   TrackingNo = null }
// ]
```

---

## 四、C 组可调用的接口

这些接口注册在 A 组自己的 `Program.cs` 里，C 组通过 DI 注入即可：

```csharp
// C 组需要注入 A 组的接口
private readonly ISupplierService _supplierService;
private readonly IProductInventoryService _productService;
```

### 4.1 查商品库存

```csharp
// IProductInventoryService.GetProductStockAsync
var result = await _productService.GetProductStockAsync("uuid-产品");
// result.IsSuccess == true → result.Data = 库存数量 (int)
// result.IsSuccess == false → result.Message = "产品库存记录不存在"
```

### 4.2 查供应商信息

```csharp
// ISupplierService.FindSupplierAccountAsync
var result = await _supplierService.FindSupplierAccountAsync(
    supplierId:    null,          // 按 ID 查
    supplierName:  "绿源",       // 按名称查（模糊匹配）
    loginAccount:  null,          // 按登录账号查
    contactPhone:  null           // 按电话查
);

// 传任意一个参数即可，传多个会叠加过滤
// result.Data = List<SupplierAccountDto>
// 每个元素：SupplierID, SupplierName, LicenseNo, ExpiryDate, CreditLevel, ContactPhone, LoginAccount
// 不含密码
```

### 4.3 验证供应商密码

```csharp
// ISupplierService.VerifySupplierPasswordAsync
var result = await _supplierService.VerifySupplierPasswordAsync("登录账号", "密码");
// result.Data == true  → 验证通过
// result.Data == false → 密码错误
// result.IsSuccess == false → 账号不存在
```

### 4.4 删除供应商

```csharp
// ISupplierService.DeleteSupplierAsync
var result = await _supplierService.DeleteSupplierAsync("uuid-供应商");
// 有关联产品时会拒绝删除
```

---

## 五、接口文件位置

| 接口 | 文件 |
|------|------|
| `IInventoryService` | `Interfaces/B组_IInventoryService.cs` |
| `ILogisticsService` | `Interfaces/B组_ILogisticsService.cs` |
| `ICommissionService` | `Interfaces/B组_ICommissionService.cs`（Dummy，C 组完成后替换） |
| `IProductInventoryService` | `Interfaces/IProductInventoryService.cs` |
| `ISupplierService` | `Interfaces/ISupplierService.cs` |

| 模型 | 文件 |
|------|------|
| `InventoryReservationItem` / `InventoryProductSnapshot` | `Models/CrossGroup/CrossGroupContracts.cs` |
| `FreightCalculationRequest` / `FulfillmentOrderRequest` 等 | `Models/CrossGroup/CrossGroupContracts.cs` |

---

## 六、完整下单流程示例

```csharp
public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request)
{
    return await _transactionManager.ExecuteAsync(async transaction =>
    {
        // ═══ 第 1 步：调 A 组锁库存 ═══
        var snapshots = await _inventoryService.ReserveAsync(
            request.Items.Select(i => new InventoryReservationItem
            {
                ProductId = i.ProductId,
                Quantity  = i.Quantity
            }).ToList(),
            transaction);

        // ═══ 第 2 步：B 组用快照计算商品金额（不信任客户端价格） ═══
        var details = snapshots.Select(s =>
        {
            var qty = request.Items.First(i => i.ProductId == s.ProductId).Quantity;
            return new BizOrderDetail
            {
                ProductId   = s.ProductId,
                ProductName = s.ProductName,
                SupplierId  = s.SupplierId,
                Quantity    = qty,
                UnitPrice   = s.UnitPrice,
                SubTotal    = s.UnitPrice * qty
            };
        }).ToList();
        var goodsAmount = details.Sum(d => d.SubTotal);

        // ═══ 第 3 步：调 A 组算运费 ═══
        var freight = await _logisticsService.CalculateFreightAsync(
            new FreightCalculationRequest
            {
                Province    = address.Province,
                City        = address.City,
                District    = address.District,
                GoodsAmount = goodsAmount,
                Items       = details.Select(d => new FulfillmentOrderItem { ... }).ToList()
            },
            transaction);

        // ═══ 第 4 步：B 组创建订单 ═══
        var order = new BizOrder
        {
            TotalAmount    = goodsAmount,
            FreightAmount  = freight,
            FinalAmount    = goodsAmount - discountAmount + freight,
            // ...
        };
        await _orderRepo.CreateOrderAsync(order, transaction);
        await _orderRepo.InsertDetailsAsync(details, transaction);

        // ═══ 第 5 步：事务提交 ═══
        // A 组的库存锁 + 运费记录 + B 组的订单，全部同时生效
        return new CreateOrderResult { OrderId = order.OrderId, ... };
    });
}
```

---

## 七、常见问题

**Q：我能在自己的方法里直接 `new OracleConnection` 查 `Inv_StockSummary` 表吗？**

**A：不能。** 铁律：不允许直接写 SQL 操作其他组的表。必须通过 A 组接口调用。

**Q：A 组的接口会自己提交事务吗？**

**A：不会。** A 组使用你传入的 `IDbTransaction`，由你控制 Commit/Rollback。

**Q：调用 ReserveAsync 后，什么时候库存会被真正扣除？**

**A：下单时只锁定（LockedQty 增加），发货完成时才真正扣除（StockQty 减少）。取消订单时释放锁定。

**Q：A 组的接口返回 `ApiResponse<T>` 还是直接抛异常？**

**A：跨组接口（IInventoryService / ILogisticsService）失败时抛异常，B 组的事务自动回滚。A 组内部接口（ISupplierService / IProductInventoryService）返回 `ApiResponse<T>`。
