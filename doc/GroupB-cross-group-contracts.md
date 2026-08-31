# GroupB 跨组接口契约

更新日期：2026-08-31

## 1. 通用事务规则

- 跨组写操作的事务由业务发起方 B 组创建、提交或回滚。
- A/C 组实现必须使用传入的 `IDbTransaction` 及其 `Connection`。
- 接口实现不得新建独立连接写数据，不得自行 `Commit` 或 `Rollback`。
- B 组只传递完成业务所需的可信快照，不允许其他组直接修改 B 组表。
- 生产注册已切换为真实适配；单元场景测试继续使用仅存在于测试项目的 Fake。

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

### 2.3 当前正式适配

`GroupAInventoryServiceAdapter` 已注册为生产实现。它复用 B 组事务，通过 A 组现有
`IProductRepository` 与 `IStockSummaryRepository` 读取可信商品并以行锁更新
`LockedQty/AvailableQty`。B 组没有在仓储或 Service 中编写 A 组表 SQL。

`ProductId`、`SupplierId` 和 `PromoterId` 均使用字符串，兼容各组的 GUID 主键。

### 2.4 商品目录与可信商品批量查询

接口：`Interfaces/IExternalCatalogServices.cs` 中的 `IGroupAProductCatalogService`。

```csharp
Task<GroupAProductSearchResult> SearchSellableProductsAsync(
    GroupAProductSearchRequest request,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<GroupATrustedProduct>> GetTrustedProductsAsync(
    IReadOnlyList<string> productIds,
    CancellationToken cancellationToken = default);
```

- B 组先从 C 组取得团长合作供应商集合，再传给 A 组查询可售商品。
- `SupplierId` 只在后端协作和下单校验中使用；消费者商品 DTO 已用 `JsonIgnore` 禁止输出该字段。
- 结算和下单必须重新批量读取实际价格、库存、上下架状态和供应商，不能信任购物车缓存。
- 结算使用库存适配返回的可信商品快照，不再使用固定商品或客户端价格。

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
    string orderId,
    IReadOnlyList<string> supplierIds,
    CancellationToken cancellationToken = default);
```

### 3.1 运费计算

- B 组传递目的省市区、商品金额及按供应商归属的可信商品快照。
- A 组按自身 `Log_FreightTemplates` 和冷链规则计算，不允许 B 组直接查询运费表。
- 返回值必须非负并符合 B 组 `NUMBER(10,2)` 金额范围。
- 当前 `GroupALogisticsServiceAdapter` 调用 A 组 `IColdChainLogisticsService.QuoteFreightAsync` 返回真实冷链运费。

### 3.2 创建物流

- B 组只允许已支付→已发货时调用。
- A 组按 `SupplierId` 创建一个或多个物流履约单元，并使用 B 组事务。
- 创建失败时 B 组订单状态保持已支付，完整事务回滚。

### 3.3 履约状态查询

- 这是只读查询，不要求加入 B 组写事务。
- A 组返回每个供应商的物流状态和可选运单号。
- B 组订单详情只展示结果，不重复持久化 `Log_ExpressDeliveries`。

### 3.4 当前 A 组联调限制

`GroupALogisticsServiceAdapter` 已把 A 组真实运费、FEFO 发货和履约状态映射到 B 组契约，
并通过 A 组工作单元挂载 B 组事务。A 组当前发货实现仍用 `AvailableQty` 再次校验已经预留
的订单；接近售罄时可能误判库存不足。该实现位于 A 组范围，B 组只记录联调问题，不直接修改。

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
- 运行时已注册 C 组真实 `CommissionService`；B 组不再保留生产 Dummy。

### 4.1 团长目录与合作范围

接口：`Interfaces/IExternalCatalogServices.cs` 中的 `IGroupCPromoterCatalogService`。

```csharp
Task<GroupCPromoterSearchResult> SearchAvailablePromotersAsync(
    GroupCPromoterSearchRequest request,
    CancellationToken cancellationToken = default);

Task<GroupCPromoterSummary?> GetPromoterAsync(
    string promoterId,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<string>> GetCooperatingSupplierIdsAsync(
    string promoterId,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<GroupCPromoterProductValidation>> ValidatePromoterProductsAsync(
    string promoterId,
    IReadOnlyList<GroupCPromoterProductCandidate> products,
    CancellationToken cancellationToken = default);
```

- C 组维护独立的团长—供应商合作关系，不要求修改 `Crm_Promoters` 字段。
- 消费者端只接收团长和商品信息，不接收供应商编号或合作关系。
- B 组下单前使用 A 组可信商品快照，再由 C 组批量校验每件商品是否允许该团长带货。
- `GroupCPromoterCatalogService` 只调用 C 组现有 `PromoterService`，用于可用团长、合作供应商、带货商品和团长售价校验；B 组不直接查询 C 组表。

### 4.2 支付与消费者消息

- 批次支付通过 C 组现有 `IPaymentService.CreatePaymentRecord` 写支付流水，B 组不再调用 C 组 Repository。
- 消息中心先由 B 组仓储查询消费者订单，再按订单调用 C 组现有 `IRefundService.GetOrderRefundsAsync`，最后在 B 组 `ConsumerMessageService` 中合并排序。
- C 组支付服务当前将审计日志写入独立连接，外层订单事务回滚时可能留下已提交日志；这是 C 组实现问题，B 组不越界修改。

## 5. C 组调用 B 组：退款积分扣回

接口：`IOrderService.DeductPointsForRefundAsync`

```csharp
Task DeductPointsForRefundAsync(
    string customerId,
    string orderId,
    int pointsToDeduct,
    CancellationToken cancellationToken = default);
```

当前行为：

- 消费者和订单 ID 必须为非空、最长 36 位字符串，且订单必须属于指定消费者。
- 订单和消费者记录会在同一事务内按固定顺序锁定。
- 同一订单只允许生成一条 `REFUND_DEDUCT` 流水；订单已经是“已退款”时重复调用直接成功返回。
- 实际扣减不超过当前积分余额、调用方请求值及订单原始奖励积分三者中的最小值。
- 已支付且尚未发货的订单会通过 A 组契约释放预留库存；已发货或已完成订单不会回补库存。
- 积分余额、流水、库存释放和订单“已退款”状态在同一事务提交或回滚。

阶段 5 仍需由 C 组完成佣金撤销和支付渠道退款，并在调用本接口前保证财务退款结果可信。
