# GroupB 跨组接口契约

更新日期：2026-07-27

## 1. 通用事务规则

- 跨组写操作的事务由业务发起方 B 组创建、提交或回滚。
- A/C 组实现必须使用传入的 `IDbTransaction` 及其 `Connection`。
- 接口实现不得新建独立连接写数据，不得自行 `Commit` 或 `Rollback`。
- B 组只传递完成业务所需的可信快照，不允许其他组直接修改 B 组表。
- 当前 Dummy 只做隔离联调，不访问其他组数据表，不代表正式适配已经完成。

## 2. B 组调用 A 组：库存预留与释放

接口：`Interfaces/IInventoryService.cs`

```csharp
Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
    IReadOnlyList<InventoryReservationItem> items,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

Task ReleaseAsync(
    FulfillmentOrderRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

### 2.1 库存预留

- `items` 已按 `ProductId` 合并并排序，数量为 1 到 9999。
- A 组必须进行防超卖条件更新或行锁校验。
- 每个输入商品必须返回且只返回一个可信商品快照。
- `ProductName`、`SupplierId`、`UnitPrice` 由 A 组提供，B 组不信任客户端价格。

### 2.2 库存释放

- 只在已支付、尚未发货订单取消时调用。
- `FulfillmentOrderRequest` 包含订单标识和原订单商品快照。
- A 组应按原预留记录幂等释放，不能根据当前商品价格重新计算。

### 2.3 当前 Dummy

| 商品ID | 商品 | 供应商ID | 单价 | 模拟可用库存 |
| --- | --- | --- | --- | --- |
| 1 | 车厘子 | 1 | 50.00 | 100 |
| 2 | 三文鱼 | 2 | 80.00 | 50 |
| 3 | 有机蔬菜 | 1 | 20.00 | 200 |

`DummyInventoryService` 只校验数量并返回确定性快照，不持久化库存变化。

## 3. B 组调用 A 组：运费与物流履约

接口：`Interfaces/ILogisticsService.cs`

```csharp
Task<decimal> CalculateFreightAsync(
    FreightCalculationRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

Task CreateShipmentAsync(
    FulfillmentOrderRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<SupplierFulfillmentStatus>> GetSupplierStatusesAsync(
    int orderId,
    IReadOnlyList<int> supplierIds,
    CancellationToken cancellationToken = default);
```

### 3.1 运费计算

- B 组传递目的省市区、商品金额及按供应商归属的可信商品快照。
- A 组按自身 `Log_FreightTemplates` 和冷链规则计算，不允许 B 组直接查询运费表。
- 返回值必须非负并符合 B 组 `NUMBER(10,2)` 金额范围。
- 当前 `DummyLogisticsService` 返回 0。

### 3.2 创建物流

- B 组只允许已支付→已发货时调用。
- A 组按 `SupplierId` 创建一个或多个物流履约单元，并使用 B 组事务。
- 创建失败时 B 组订单状态保持已支付，完整事务回滚。

### 3.3 履约状态查询

- 这是只读查询，不要求加入 B 组写事务。
- A 组返回每个供应商的物流状态和可选运单号。
- B 组订单详情只展示结果，不重复持久化 `Log_ExpressDeliveries`。

### 3.4 远端 A 组现状

`origin/dev-groupA` 当前 `ILogisticsService` 使用 EF、自有订单表和另一套 1-5 状态码，
无法直接实现上述 Oracle/Dapper 事务契约。正式合并前需要 A 组提供适配器，不能把现有
Service 直接注册到 B 组。

## 4. B 组调用 C 组：订单完成佣金登记

接口：`Interfaces/ICommissionService.cs`

```csharp
Task RegisterCompletedOrderAsync(
    CommissionOrderRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

- 只允许已发货→已完成时调用。
- B 组传递订单、消费者、可选团长、实付佣金基数和完成时间。
- C 组负责阶梯佣金算法、预计佣金和审计记录，不允许 B 组直接写 C 组表。
- C 组失败时订单保持已发货，完整事务回滚。
- `origin/dev-groupC` 当前尚无佣金接口，现阶段使用 `DummyCommissionService`。

## 5. C 组调用 B 组：退款积分扣回

接口：`IOrderService.DeductPointsForRefundAsync`

```csharp
Task DeductPointsForRefundAsync(
    int customerId,
    int orderId,
    int pointsToDeduct);
```

当前行为：

- 参数必须为正数。
- 消费者记录会在事务内锁定。
- 实际扣减不超过当前积分余额。
- 积分余额与 `REFUND_DEDUCT` 流水在同一事务提交。

阶段 5 还需补充：

- 按 `orderId` 幂等校验，避免重复退款重复扣分。
- B 组调用 C 组佣金撤销接口并统一处理失败回滚。
- 完整退款状态 5→6 和支付退款结果协同。
