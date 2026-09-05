# 独立冷链物流商模拟器

这是独立启动的 ASP.NET Core MVC + Bootstrap 应用，用于在本机模拟外部物流商接单和上报轨迹。它通过受控 HTTP 接口读取 Oracle 中物流相关的真实订单及已交接运单，并把操作写回真实履约链路；页面没有固定数组，也不会生成示例订单。

## 本机启动

在仓库根目录使用 PowerShell 7 运行：

```powershell
pwsh -NoProfile -File scripts/Start-CarrierDemo.ps1
```

需要 .NET 10 SDK、可用的 Oracle 连接配置，且当前 schema 已执行 `groupA_logistics_persistence.sql`。脚本不执行 DDL，不创建任何演示订单或运单。

- 商城：`http://localhost:5064/app/`
- 物流商模拟器：`http://localhost:5077/`
- 运行日志：`tmp/carrier-demo/*.log`
- 保持启动终端运行；`Ctrl+C` 会关闭本次启动的两个进程。

## 如何产生可演示的真实运单

1. 消费者完成一张真实订单的下单和支付。
2. 启动模拟器，用订单编号查询，在已支付订单上点击“接单生成运单”。该操作调用真实供应商履约事务，会扣减已锁定库存、生成 Oracle 运单并按多供应商履约情况推进订单状态。
3. 生成后选择运单，上报发生时间、位置、温度、状态和说明，再回商城订单详情刷新物流。

列表按订单的供应商包裹展示。已支付且尚未发货的订单可以通过“接单生成运单”进入真实履约流程；已存在 `Log_ExpressDeliveries` 的包裹可进入详情并上报轨迹。历史完成但缺少运单的数据仍保持只读，不会补造历史轨迹。

## 接口与安全范围

```text
GET  /api/demo-carrier/shipments?keyword=<运单号或订单编号>
GET  /api/demo-carrier/shipments/{deliveryId}
POST /api/demo-carrier/shipments/handoffs
POST /api/demo-carrier/shipments/{deliveryId}/events
```

接单字段为 `orderId`、`supplierId`，后台会再次核验真实订单状态、商品归属和授权范围。上报字段为 `eventId`、`statusCode`、`occurredAt`、`location`、`temperatureCelsius`、`description`。`eventId` 必须稳定：重试时使用同一编号和载荷，内容变化必须使用新事件。后台从运单确定订单和供应商，不信任外部传入的归属。

默认接口关闭。只有 `Development`、Oracle Provider、`GroupA:DemoCarrier:Enabled=true` 和至少 32 字符的独立 `ApiKey` 同时满足才可用。启动脚本在本机演示期间启用 `IncludeAllDatabaseShipments=true`，因此页面上的提交会直接更新选中的 Oracle 真实运单。若模拟单一物流商，应关闭该选项并用 `SupplierIds` 限制范围。

这不是生产物流接入：真实物流公司仍需独立 API/回调、每家凭据与权限、HTTPS、密钥轮换、审计和重试运维。

## 验证

```powershell
dotnet run --project tests/FreshColdChain.Tests -c Release
pwsh -NoProfile -File tests/verify-groupb-boundaries.ps1
pwsh -NoProfile -File tests/verify-groupa-logistics-ddl.ps1
```

2026-09-05：Oracle 中的专用示例运单、订单和关联测试主体已删除，一键造数入口同步移除。模拟器现在只使用真实订单；点击接单会执行真实发货事务，并非生成独立示例数据。
