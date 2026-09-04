# 独立冷链物流商模拟器

这是独立启动的 ASP.NET Core MVC + Bootstrap 应用，不是商城内嵌页面，也不是真实物流公司服务。模拟器通过受控 HTTP 接口读写商城的 Oracle 物流数据，不持有数据库账号、不绕过状态机直接执行 SQL。

## 本机演示

使用 PowerShell 7，在仓库根目录运行：

```powershell
pwsh -NoProfile -File scripts/Start-CarrierDemo.ps1 -Seed
```

需要 .NET 10 SDK、可用的现有 Oracle 连接配置，以及已执行 `groupA_logistics_persistence.sql` 的数据库。脚本不自动执行 DDL，不会关闭已有服务；端口被占用时先关闭原服务，或传入不同的 `-ShopPort` / `-CarrierPort`。不要同时启动多个商城后台实例。

- 商城：`http://localhost:5064/app/`
- 物流商：`http://localhost:5077/`
- 本机演示账号：`tmp/carrier-demo/consumer-access.json`，密码随机生成，不提交 Git。
- 两个服务的日志：`tmp/carrier-demo/*.log`，文件名含进程编号。
- 保持启动终端运行；Ctrl+C 会关闭本次启动的两个进程。再次启动会生成新的接口密钥。

`-Seed` 只新增专用演示账号、地址、禁用供应商、下架商品和 3 张零金额运单，不覆盖已有演示数据，不产生支付记录或扣减真实库存。编号冲突会报错并回滚。重复运行不会重置已演示的物流状态。

## 演示步骤

1. 在商城用上述专用账号登录，进入“我的订单”，选择 `ORDER-DEMO-01` 等测试订单。
2. 在模拟器查询 `DEMO-001` 等运单，查看当前轨迹；选择状态，填写发生时间、位置、温度和说明，提交。
3. 返回商城订单详情，点击“刷新状态”，可看到同一条已提交轨迹。订单状态“已发货”与包裹状态“派送中”是不同维度，不要求两者文案相同。
4. 正常演示可从“已发货 → 运输中 → 派送中 → 已签收”推进。冷藏默认范围 0～8℃；填写 15℃ 会记录异常并阻止签收。异常可通过正常温度的“运输中”事件恢复，历史异常保留。支持退回中、已退回；非法状态回退或逆序/未来事件会被拒绝。
5. 只演示物流，不对这些零金额测试订单发起退款或财务验收。已签收是物流终态，不能通过重新运行 Seed 回退。

验收后保留的示例状态：`DEMO-001` 派送中、`DEMO-002` 已发货、`DEMO-003` 物流异常（15℃）。后续操作会继续推进这些记录。

## 接口与安全范围

商城接口：

```text
GET  /api/demo-carrier/shipments?keyword=DEMO-001
GET  /api/demo-carrier/shipments/{deliveryId}
POST /api/demo-carrier/shipments/{deliveryId}/events
```

上报字段为 `eventId`、`statusCode`、`occurredAt`、`location`、`temperatureCelsius`、`description`。`eventId` 必须稳定：重试使用同一编号和载荷；内容改变必须是新事件。后台从运单确定订单和供应商，不信任外部传入的归属。事件、元数据及基础物流状态共用事务与行锁。

默认接口关闭。只有 `Development`、Oracle Provider、`GroupA:DemoCarrier:Enabled=true`、至少 32 字符的独立 `ApiKey` 和明确 `SupplierIds` 白名单同时满足才可用。请求需要 `X-Carrier-Key`；启动脚本仅授权 `SUP-CARRIER-DEMO`。密钥只在服务端进程环境中传递，不进入浏览器。模拟器自身只允许本机 IP/Host，并使用防伪令牌保护写入。

这不是生产接入方案：真实物流公司仍需外部 API/回调接入、每家独立凭据与权限、HTTPS、密钥轮换、审计及重试运维。这里不开放公网、不提供任意 SQL 或删除历史轨迹功能。

## 验证

```powershell
dotnet run --project tests/FreshColdChain.Tests -c Release
dotnet run --project tests/CarrierDemoFixture -c Release -- --verify
pwsh -NoProfile -File tests/verify-groupb-boundaries.ps1
pwsh -NoProfile -File tests/verify-groupa-logistics-ddl.ps1
```

`--verify` 使用 `DEMO-002` 做可回滚事件校验，验证 Oracle 亚秒精度、同事件幂等、回滚和供应商隔离，不覆盖已提交记录；请在其仍可接受“运输中”状态时运行。

2026-09-05 验收：98 个场景通过，主应用及模拟器构建零警告；真实 Oracle 校验通过；内置浏览器验证搜索、上报、状态回退拒绝、温控异常、商城同步显示和进程重启后持久性。未把这些物流演示测试等同于真实支付、库存扣减或外部物流商生产验收。

### 界面验收记录

按 frontend-app-builder 技能先生成概念再实现，沿用项目 MVC / Bootstrap，不新增前端框架。概念文件位于本机 `C:/Users/DC/.codex/generated_images/01a0623b-5c8d-7b00-bddd-443da8075b90/exec-08288bd7-d5cf-4e1d-9a6e-104e417cc684.png`，只作设计参考，不嵌入页面。

内置浏览器完成交互验收，但尺寸覆盖后的截图缩放不稳定，因此使用 Playwright CLI 补拍 1505×1045 桌面和 390×844 手机截图，并以 `view_image` 对照概念和最终截图。核对了深蓝页头与浅灰底色、左右运单/详情双栏、标题和表单字体层级、白色边框面板间距、青色选中行及主按钮、时间线与表单顺序；手机使用单栏且无横向溢出。

文案差异与必要偏差：测试供应商、日期、轨迹内容使用真实数据库值；新增查询上限、安全/状态说明、空结果与错误提示；不虚构概念里的未来空白轨迹点，完整展示实际事件，所以较长轨迹需要滚动。操作保留文字按钮和简洁轨迹节点，未引入图标依赖。布局及主要交互忠实于概念，未发现影响演示的布局问题。浏览器额外请求的 favicon 返回 404，不影响页面或业务资源。
