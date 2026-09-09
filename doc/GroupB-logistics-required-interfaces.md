# B 组物流体系正式对接接口文档

版本：1.2

日期：2026-09-04
调用方：B 组订单与履约模块  
协作方：A 组库存/冷链物流、C 组统一认证与退款

## 1. 对接范围与优先级

| 优先级 | 提供方 | 接口 | 当前状态 | 正式上线要求 |
| --- | --- | --- | --- | --- |
| P0 | A 组 | 结构化运费报价 | 已接基础报价，B 组适配补充结构 | A 组直接返回规则摘要、数据源和计费项 |
| P0 | A 组 | 供应商级发货登记 | 已接 Oracle Provider，待隔离库迁移验收 | 持久化承运商、外部运单、温区和预计送达 |
| P0 | A 组 | 供应商级完整物流快照 | 已接 Oracle 仓储读取，待真实进程重启验证 | 返回事件、温度、异常和稳定状态代码 |
| P0 | A 组 | 追加物流轨迹事件 | 已实现事件哈希、唯一约束与外部事务，待真实并发验证 | 按 `EventId` 幂等持久化并参与 B 组事务 |
| P0 | C 组 | 统一角色/权限判定 | 已接 IGroupCAuthorizationService 与逐动作权限 | 部署时确认实际角色 ID 的授权映射，并验证停用与拒绝路径 |
| P1 | C 组 | 物流责任退款证据 | 已有 `LiabilityType=Logistics` | 可选扩展结构化物流异常证据 |

P0 接口未正式接入时，`FallbackGroupALogisticsExtensionProvider` 仅用于开发联调和演示，不能作为生产物流记录来源。

## 2. 通用约定

- 所有 ID 使用字符串，最大 36 个字符；禁止转换为数值。
- 金额使用 `decimal`，数据库精度与 B 组 `NUMBER(10,2)` 一致。
- 状态代码固定为 `PENDING`、`PACKING`、`SHIPPED`、`IN_TRANSIT`、
  `OUT_FOR_DELIVERY`、`DELIVERED`、`EXCEPTION`、`RETURNING`、`RETURNED`。
- 时间通过 JSON 传输时使用带时区的 ISO 8601；当前进程内契约使用 `DateTime`，双方部署时统一为 `Asia/Shanghai`。
- 跨组写操作由 B 组开启、提交和回滚事务。A/C 组必须使用传入 `IDbTransaction.Connection`，不得新建连接写入，不得自行提交或回滚。
- `CancellationToken` 必须向下透传。可重试故障不得转换为业务成功。
- 服务端日志可记录 `OrderId`、`SupplierId`、`DeliveryId`、`EventId` 和 `traceId`，不得记录收货电话、完整地址等敏感信息。

## 3. A 组 P0 接口

A 组已实现 `OracleGroupALogisticsExtensionProvider` 并设为 DI 默认项；迁移顺序和未完成验收见 `groupA-logistics-persistence.md`。B 组重复事件仍须交给 A 组验证载荷，不能只按编号短路成功。

### 3.1 结构化运费报价

```csharp
Task<FreightCalculationResult> QuoteFreightAsync(
    FreightCalculationRequest request,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

请求必需字段：

| 字段 | 说明 |
| --- | --- |
| `Province/City/District` | B 组已校验的收货地址快照 |
| `GoodsAmount` | 服务端可信商品金额 |
| `Items[].ProductId` | A 组商品 ID |
| `Items[].SupplierId` | A 组供应商 ID，仅用于后端聚合，不输出给消费者 |
| `Items[].Quantity/UnitPrice/SubTotal` | B 组基于 A 组可信商品快照形成的计费输入 |

响应必须包含 `SchemaVersion`、`FreightAmount`、`GoodsAmount`、目的省市区、`RuleSummary`、
`CalculatedAt`、`DataSource=GROUP_A` 和计费项。A 组负责按“供应商 + 温区 + 命中模板”聚合包裹，首重费、续重费和包装费均由 A 组解释；B 组只保存结果快照，不重新计算。

### 3.2 供应商级发货登记

```csharp
Task<SupplierLogisticsSnapshot> RegisterShipmentAsync(
    LogisticsShipmentRegistration registration,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

`registration` 必需包含：

- A 组基础发货产生的 `DeliveryId`、`BaseTrackingNo`、`BaseStatus` 和 `ShippedAt`。
- B 组订单 `OrderId` 与当前履约单元 `SupplierId`。
- `Command` 中的 `CarrierCode`、`CarrierName`、可选外部 `TrackingNo`、
  `PackageTemperature`、`EstimatedArrivalAt` 和 `Remark`。

幂等键为 `(OrderId, SupplierId)`。相同键重复登记必须返回既有结果，不得重复扣库存、重复生成发货单或重复创建首条轨迹。若外部运单号冲突，应返回明确业务错误。

### 3.3 查询供应商级完整物流快照

```csharp
Task<SupplierLogisticsSnapshot> GetSnapshotAsync(
    LogisticsTraceSeed seed,
    CancellationToken cancellationToken = default);
```

响应字段：

| 分类 | 字段 |
| --- | --- |
| 身份 | `OrderId`、`SupplierId`、`DeliveryId` |
| 承运 | `CarrierCode`、`CarrierName`、`TrackingNo`、`PackageTemperature` |
| 状态 | `StatusCode`、`ShippedAt`、`EstimatedArrivalAt`、`DeliveredAt` |
| 异常 | `HasException`、`ExceptionMessage` |
| 轨迹 | `Events[].EventId/StatusCode/Location/Description/OccurredAt/TemperatureCelsius/IsTemperatureException` |
| 来源 | 正式实现固定返回 `DataSource=GROUP_A` |

事件按 `OccurredAt` 升序返回；同一 `EventId` 只能出现一次。没有发货单时返回 `PENDING` 空快照，不应以 500 表示正常的“待发货”。

### 3.4 追加物流轨迹事件

```csharp
Task<SupplierLogisticsSnapshot> AppendTrackingEventAsync(
    LogisticsTrackingEventCommand command,
    IDbTransaction transaction,
    CancellationToken cancellationToken = default);
```

`command.EventId` 是调用方生成的幂等键。相同 `EventId` 重试时必须返回当前快照且不得新增第二条事件。A 组应建立唯一约束，至少覆盖 `EventId`；推荐同时校验事件所属 `OrderId + SupplierId` 不可漂移。

B 组在调用前校验供应商归属、订单状态和物流状态机。A 组仍应防御性校验发货单归属及状态，不能接受跨订单或跨供应商更新。温控判定规则应由 A 组商品温区/冷链配置决定，并在异常时返回 `StatusCode=EXCEPTION`、`HasException=true` 和可展示原因。

### 3.5 A 组业务错误

建议使用稳定错误代码并附中文消息：

| 错误代码 | 含义 | B 组处理 |
| --- | --- | --- |
| `LOGISTICS_ORDER_NOT_FOUND` | 订单或发货单不存在 | 业务失败，事务回滚 |
| `LOGISTICS_SUPPLIER_MISMATCH` | 发货单不属于该供应商 | 拒绝并记录安全日志 |
| `LOGISTICS_INVALID_TRANSITION` | 状态跳转非法 | 提示刷新物流状态 |
| `LOGISTICS_TRACKING_CONFLICT` | 运单号被其他履约单元占用 | 提示供应商修正 |
| `LOGISTICS_EVENT_CONFLICT` | 同一事件 ID 的载荷不一致 | 拒绝，禁止覆盖原事件 |
| `LOGISTICS_INVENTORY_SHORTAGE` | FEFO 发货时库存不足 | 订单保持 `PAID` |
| `LOGISTICS_TEMP_OUT_OF_RANGE` | 运输温度越界 | 保存事件并返回异常快照 |

## 4. C 组 P0 统一权限接口

当前已实现 `IGroupCAuthorizationService`。B 组读取统一登录写入的 `AdminId` 或 `SupplierId` Session，逐动作调用 C 组服务，不读取 C 组用户/权限表：

```csharp
Task<GroupCAuthorizationResult> AuthorizeAsync(
    string subjectId,
    string permissionCode,
    string? resourceId = null,
    CancellationToken cancellationToken = default);
```

权限代码至少包括：

| 权限 | 用途 |
| --- | --- |
| `groupb.orders.read` | 管理员查看 B 组订单与物流汇总 |
| `groupb.orders.manage` | 管理员执行合法订单管理动作 |
| `groupb.fulfillment.read` | 供应商查看本人履约单元 |
| `groupb.fulfillment.write` | 供应商登记发货、追加本人轨迹 |

返回结果至少包含 `IsAllowed`、`SubjectId`、`RoleCode` 和可选拒绝原因。供应商数据范围仍由 B 组使用当前登录的 `SupplierId` 对订单明细做二次校验，不能只依赖前端字段或权限接口返回的 `resourceId`。

实现约定：

- 角色权限位于 `GroupC:Authorization:RolePermissions`，键为真实 `SYS_ROLES.RoleId`，不是角色名称。已有模型没有权限表，本次不增加数据库表、不引入认证框架。
- 默认只为 C 组管理员注册使用的 `r_admin` 显式授予订单读/管理权限；其他角色必须配置，不自动继承权限。只读角色只配置 `groupb.orders.read`。
- C 组每次从自己的仓储检查管理员和角色存在且状态为 `Enable`/`Enabled`，不把登录时的角色快照当作永久授权。未知状态一律拒绝。
- 供应商读写权限由 `SupplierPermissions` 显式配置，同时通过 A 组服务检查账号状态 `Active` 和资质未过期。`resourceId` 必须等于当前供应商 ID；具体订单归属仍由履约服务校验。
- `OrderController` 和 `SupplierFulfillmentController` 的每个动作都有 `GroupBPermissionAttribute`。未声明动作、身份不匹配或权限不满足返回 403；授权故障返回 503，不降级为仅校验 Session。
- 新管理员登录写入 `AdminId`；只有 `AdminName` 的旧会话必须重新登录。此次入口控制范围是上述 B 组后台，不代表其他组所有后台入口已完成权限审计。

## 5. C 组 P1 物流责任退款协作

现有 `IRefundService` 和 `GroupC_RefundRequest.LiabilityType` 已能使用 `Logistics` 标记物流责任，因此本阶段不要求新增退款主流程。建议后续增加结构化证据字段：

```text
DeliveryId, TrackingNo, LogisticsEventId, ExceptionType,
TemperatureCelsius, OccurredAt, EvidenceDataSource
```

只有 `EvidenceDataSource=GROUP_A` 的正式事件可用于自动责任判定；`FALLBACK` 只能作为人工审核提示。C 组完成资金退款和佣金冲回后，再调用 B 组积分扣回接口，保持现有事务顺序。

## 6. 替换兜底 Provider 的步骤

1. A 组实现 `IGroupALogisticsExtensionProvider`，保持上述 DTO 与状态代码不变。
2. 在 `GroupBServiceCollectionExtensions` 中把 `FallbackGroupALogisticsExtensionProvider` 的注册替换为 A 组正式实现。
3. 保留 `Fallback` 配置但在生产环境禁用；正式返回必须是 `DataSource=GROUP_A`。
4. 执行 A 组适配、供应商履约、订单生命周期、边界和 DDL 全部场景测试。
5. 在隔离 Oracle schema 验证同一发货请求和同一 `EventId` 重放不会产生重复数据。
6. 验证 A 组写入失败时，B 组订单状态、库存扣减、发货单和轨迹全部回滚。

## 7. 验收清单

- 多供应商订单分别生成履约单元，全部发货后 B 组订单才进入 `SHIPPED`。
- 相同 `(OrderId, SupplierId)` 发货重试不重复扣库存。
- 相同 `EventId` 重试只保留一条轨迹。
- 冷藏、冷冻温度越界和预计送达超时能产生明确异常。
- 消费者响应不包含 `SupplierId`，只在包裹 `DELIVERED` 后开放对应商品确认收货。
- A 组写入与 B 组订单状态在同一事务提交或回滚。
- 未登录、越权供应商和无管理权限用户均无法访问履约入口。
- 生产响应不出现 `DataSource=FALLBACK`。
