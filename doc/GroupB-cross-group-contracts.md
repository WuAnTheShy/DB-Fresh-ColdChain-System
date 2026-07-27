# GroupB 跨组接口契约

更新日期：2026-07-27

## 1. B 组调用 A 组：库存预留

接口：`Interfaces/IInventoryService.cs`

```csharp
Task<IReadOnlyList<InventoryProductSnapshot>> ReserveAsync(
    IReadOnlyList<InventoryReservationItem> items,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

### 1.1 调用方保证

- B 组在调用前已经开启 Oracle 事务。
- `items` 已按 `ProductId` 合并并排序。
- 每个 `ProductId` 大于 0，每个 `Quantity` 在 1 到 9999 之间。
- B 组负责最终 `Commit` 或 `Rollback`。

### 1.2 A 组实现要求

- 必须使用传入的 `transaction.Connection` 查询商品并扣减或锁定库存。
- 不得新建独立连接执行库存写入。
- 不得在接口内部提交或回滚事务。
- 必须进行防超卖条件更新或行锁校验。
- 必须为每个输入商品返回且只返回一个 `InventoryProductSnapshot`。
- 返回的 `ProductName`、`SupplierId`、`UnitPrice` 是订单明细的服务端可信快照。
- 商品不存在、下架或库存不足时抛出业务异常，B 组会回滚完整订单事务。

### 1.3 Dummy 说明

当前 `DummyInventoryService` 只用于 A 组事务型适配完成前的隔离联调：

| 商品ID | 商品 | 供应商ID | 单价 | 模拟可用库存 |
| --- | --- | --- | --- | --- |
| 1 | 车厘子 | 1 | 50.00 | 100 |
| 2 | 三文鱼 | 2 | 80.00 | 50 |
| 3 | 有机蔬菜 | 1 | 20.00 | 200 |

Dummy 不访问 A 组数据表，也不持久化库存变化，不可用于最终生产或答辩数据库演示。
A 组实现接口后，只需将依赖注入中的 `DummyInventoryService` 替换为正式实现。

## 2. C 组调用 B 组：退款积分扣回

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
- 接收 C 组佣金撤销结果并统一处理失败回滚。
