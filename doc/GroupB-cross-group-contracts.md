# GroupB 跨组接口契约

更新日期：2026-09-04

## 1. 通用事务规则

- 跨组写操作的事务由业务发起方创建、提交或回滚：下单由 B 组控制，财务退款由 C 组控制。
- A/C 组实现必须使用传入的 `IDbTransaction` 及其 `Connection`。
- 接口实现不得新建独立连接写数据，不得自行 `Commit` 或 `Rollback`。
- B 组只传递完成业务所需的可信快照，不允许其他组直接修改 B 组表。
- 生产注册已切换为真实适配；单元场景测试继续使用仅存在于测试项目的 Fake。

## 2. B 组调用 A 组：库存校验与真实扣减

接口：`Interfaces/IGroupAInventoryGateway.cs`（B 组内部适配入口，不改动既有共享 `IInventoryService` 契约）

```csharp
Task<IReadOnlyList<InventoryProductSnapshot>> CheckAvailabilityAsync(
    IReadOnlyList<InventoryAvailabilityItem> items,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

### 2.1 下单校验

- `items` 已按 `ProductId` 合并并排序，数量为 1 到 9999。
- B 组适配器只调用 A 组 `IProductInventoryService` 和 `ISupplierService`，不依赖 A 组 Repository。
- 每个输入商品必须返回且只返回一个可信商品快照。
- `ProductName`、`SupplierId`、`UnitPrice` 由 A 组提供，B 组不信任客户端价格。
- 下单阶段不修改 `LockedQty`，所以取消或支付超时无需跨组释放库存。

### 2.2 发货扣减

- 已支付订单进入发货时，B 组通过 `ILogisticsService.CreateShipmentAsync` 调用 A 组真实冷链服务。
- A 组在 B 组事务中执行库存行锁、FEFO 批次扣减、库存汇总更新、发货单和溯源记录写入。
- A 组发货校验是最终库存裁决；若下单后库存发生变化，发货失败并使 B 组订单保持 `PAID`。

### 2.3 当前正式适配

`GroupAInventoryServiceAdapter` 已注册为生产实现。它复用 B 组事务，通过 A 组现有
`IProductInventoryService.GetProductByIdAsync/GetInventoryAsync` 和
`ISupplierService.GetAllSuppliersAsync` 读取可信商品、库存和供应商信息。B 组不直接调用
A 组 Repository，也不编写或执行 A 组表 SQL。

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
Task<FreightCalculationResult> QuoteFreightAsync(
    FreightCalculationRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

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

Task<SupplierLogisticsSnapshot> CreateSupplierShipmentAsync(
    FulfillmentOrderRequest request,
    SupplierShipmentCommand command,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<SupplierLogisticsSnapshot>> GetSupplierLogisticsAsync(
    string orderId,
    IReadOnlyList<string> supplierIds,
    CancellationToken cancellationToken = default);

Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
    LogisticsTrackingEventCommand command,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

### 3.1 运费计算

- B 组传递目的省市区、商品金额及按供应商归属的可信商品快照。
- A 组按自身 `Log_FreightTemplates` 和冷链规则计算，不允许 B 组直接查询运费表。
- 返回值必须非负并符合 B 组 `NUMBER(10,2)` 金额范围。
- 当前 `GroupALogisticsServiceAdapter` 调用 A 组 `IColdChainLogisticsService.QuoteFreightAsync` 返回真实冷链运费。
- `FreightCalculationResult` 同时返回目的地、货值、规则摘要、计算时间、数据源和商品计费项。
- B 组将完整结果序列化到 `Biz_Orders.FreightQuoteSnapshot`，但不解释或重新计算 A 组规则。
- A 组后续应在自身实现中按“供应商 + 温区 + 命中模板”聚合重量，并确保首重费和包装费按包裹收取；该算法不在 B 组实现。

### 3.2 创建物流

- B 组只允许已支付→已发货时调用。
- A 组按 `SupplierId` 创建一个或多个物流履约单元，并使用 B 组事务。
- 创建失败时 B 组订单状态保持已支付，完整事务回滚。

### 3.3 履约状态查询

- 这是只读查询，不要求加入 B 组写事务。
- A 组返回每个供应商的物流状态和可选运单号。
- B 组订单详情只展示结果，不重复持久化 `Log_ExpressDeliveries`。

### 3.4 当前正式适配

`GroupALogisticsServiceAdapter` 已把 A 组真实运费、FEFO 发货和履约状态映射到 B 组契约，
并通过 A 组工作单元挂载 B 组事务。履约状态调用
`IColdChainLogisticsService.GetTraceabilityByOrderAsync`，不再直接读取 A 组物流 Repository。

### 3.5 高级物流扩展接口与兜底

A 组当前公开接口尚不能接收承运商、外部运单号、预计送达时间、轨迹事件和运输温度。
B 组因此新增 `IGroupALogisticsExtensionProvider`，但不修改 A 组现有接口和数据表：

```csharp
Task<SupplierLogisticsSnapshot> RegisterShipmentAsync(
    LogisticsShipmentRegistration registration,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);

Task<SupplierLogisticsSnapshot> GetSnapshotAsync(
    LogisticsTraceSeed seed,
    CancellationToken cancellationToken = default);

Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
    LogisticsTrackingEventCommand command,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

当前 `FallbackGroupALogisticsExtensionProvider` 的约束：

- 仅在进程内保存模拟扩展数据，不写任何 A/B/C 业务表。
- 承运商、发货地点、描述、时效和温区阈值均来自 `GroupB:LogisticsFallback` 配置。
- 所有结果明确标记 `DataSource=FALLBACK`，调用方不得将其误认为正式承运商回传数据。
- A 组提供正式能力后，新建 Provider 实现并替换 DI 注册，B 组订单与页面无需改写。
- 正式实现的写操作必须使用 B 组传入事务，禁止自行提交或回滚。
- B 组在调用 `AppendTrackingEventAsync` 前执行物流状态机校验和供应商归属校验。
- `LogisticsTrackingEventCommand.EventId` 由 B 组生成；正式实现必须按该字段幂等，重复请求不得新增事件。
- 兜底实现按配置温区阈值识别温控异常，并在超过预计送达时间后生成延误异常。
- A 组正式实现应返回稳定事件 ID，并对相同事件请求提供幂等保护。

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
    CancellationToken cancellationToken = default,
    IDbTransaction? externalTransaction = null);

Task DeductPointsForPartialRefundAsync(
    string customerId,
    string orderId,
    int pointsToDeduct,
    CancellationToken cancellationToken = default,
    IDbTransaction? externalTransaction = null);
```

当前行为：

- 提供外部事务时，B 组直接复用该事务，成功或异常均不提交、回滚或释放它；失败异常交由发起方处理。
- 未提供外部事务时保留 B 组自有事务行为，兼容旧调用方，但不能保证与 C 组退款原子提交。
- 当前 C 组 `RefundService` 尚未传入自身事务。必须在整单和部分退款调用中传入 `externalTransaction: _uow.Transaction`，并确保事务有效、任何失败都由 C 组整体回滚；仅更新 B 组接口不代表跨组退款已修复。
- 消费者和订单 ID 必须为非空、最长 36 位字符串，且订单必须属于指定消费者。
- 订单和消费者记录会在同一事务内按固定顺序锁定。
- 同一订单只允许生成一条 `REFUND_DEDUCT` 流水；订单已经是“已退款”时重复调用直接成功返回。
- 实际扣减不超过当前积分余额、调用方请求值及订单原始奖励积分三者中的最小值。
- 未发货订单尚未扣减 A 组真实库存，退款不执行库存写操作；已发货或已完成订单也不会回补库存。
- 积分余额、流水和订单“已退款”状态在同一事务提交或回滚。

阶段 5 仍需由 C 组完成佣金撤销和支付渠道退款，并在调用本接口前保证财务退款结果可信。
