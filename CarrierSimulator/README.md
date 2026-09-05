# 独立冷链物流商模拟器

这是独立启动的 ASP.NET Core MVC + Bootstrap 应用，用于在本机模拟外部物流商上报轨迹。它通过受控 HTTP 接口读取 Oracle 中的真实发货单，并把操作写回选中的真实运单；页面没有固定数组，也不会自动生成测试运单。

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
2. 对应供应商登录商城，进入“待发货订单”，对该订单执行发货，填写真实的承运商、运单号及冷链信息。
3. 启动模拟器，用运单号或订单编号查询。
4. 选择运单后上报发生时间、位置、温度、状态和说明，再回商城订单详情刷新物流。

当 Oracle `Log_ExpressDeliveries` 没有真实发货单时，模拟器会如实显示空列表。不要为已存在但缺少发货元数据的订单伪造承运商、运单号或轨迹；应由真实供应商履约流程生成。

## 接口与安全范围

```text
GET  /api/demo-carrier/shipments?keyword=<运单号或订单编号>
GET  /api/demo-carrier/shipments/{deliveryId}
POST /api/demo-carrier/shipments/{deliveryId}/events
```

上报字段为 `eventId`、`statusCode`、`occurredAt`、`location`、`temperatureCelsius`、`description`。`eventId` 必须稳定：重试时使用同一编号和载荷，内容变化必须使用新事件。后台从运单确定订单和供应商，不信任外部传入的归属。

默认接口关闭。只有 `Development`、Oracle Provider、`GroupA:DemoCarrier:Enabled=true` 和至少 32 字符的独立 `ApiKey` 同时满足才可用。启动脚本在本机演示期间启用 `IncludeAllDatabaseShipments=true`，因此页面上的提交会直接更新选中的 Oracle 真实运单。若模拟单一物流商，应关闭该选项并用 `SupplierIds` 限制范围。

这不是生产物流接入：真实物流公司仍需独立 API/回调、每家凭据与权限、HTTPS、密钥轮换、审计和重试运维。

## 验证

```powershell
dotnet run --project tests/FreshColdChain.Tests -c Release
pwsh -NoProfile -File tests/verify-groupb-boundaries.ps1
pwsh -NoProfile -File tests/verify-groupa-logistics-ddl.ps1
```

2026-09-05：Oracle 中的专用示例运单、订单和关联测试主体已删除，一键造数入口同步移除。模拟器现在只展示和修改真实业务流程创建的运单。
