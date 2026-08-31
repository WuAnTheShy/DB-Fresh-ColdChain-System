# GroupB 完成情况

更新日期：2026-08-31

## 1. 职责边界

B 组负责以下 8 张核心表：

- `Crm_Customers`
- `Crm_UserAddresses`
- `Crm_MemberLevels`
- `Mkt_Coupons`
- `Mkt_CouponRecords`
- `Crm_PointLogs`
- `Biz_Orders`
- `Biz_OrderDetails`

此外，B 组为“每月自动定级留痕”维护 1 张扩展表：

- `Crm_MemberLevelHistories`

`Crm_Promoters`、支付、退款、审计、商品、库存和物流均由其他组负责。B 组只通过接口交换字符串标识和可信快照，不在 `groupB_ddl.sql` 中创建其他组的表。

## 2. 当前完成度

| 领域 | 当前结果 | 状态 |
| --- | --- | --- |
| 消费者 | 注册、统一登录、资料查询与受限编辑；密码仅以 `PasswordHasher` 哈希持久化 | 已完成 |
| 地址 | CRUD、唯一默认地址、删除默认地址后自动顺延 | 已完成 |
| 会员 | 按累计消费动态定级，下单时按生效等级计算积分倍率 | 已完成 |
| 优惠券 | 活动券查询、原子领券、防重复、防超发、下单核销与取消归还 | 已完成 |
| 积分 | 下单发放、取消/退款扣回、余额与不可缺失流水同事务提交 | 已完成 |
| 订单 | 服务端可信计价、地址快照、供应商拆单、列表/详情与 7 状态字符串契约 | 已完成 |
| 退款 | 未发货退款释放库存；已发货/已完成退款不回补库存；重复调用幂等 | 已完成 |
| 主键 | B 组 8 张核心表及定级历史扩展表的主键和内部外键均为 `VARCHAR2(36)` | 已完成 |
| DDL | 约束、索引、外键、`CouponType` 和覆盖全部 B 组物理表的最小演示数据 | 已完成 |
| Vue 消费者端 | 客户、地址、优惠券、订单 API 已接入；字符串 ID 端到端透传 | 已完成 |
| A/C 正式适配 | 已注册 A 组真实库存/物流适配、C 组佣金/支付/团长/退款服务，已移除运行时 Dummy | 已完成 |
| Oracle 集成验证 | 缺少隔离测试库，未对未知共享库执行写入测试 | 环境待办 |

## 3. 本轮优化记录

| 提交 | 内容 |
| --- | --- |
| `9664b55` | 修复前端 `nanoid` 高危依赖漏洞 |
| `17f9a7a` | 完善退款幂等与发货前后库存处理 |
| `a8866bd` | 统一订单 7 状态字符串契约 |
| `319bcea` | 统一跨组商品、供应商、团长字符串标识 |
| `76b0316` | 统一 B 组主键为字符串 GUID，并清理 B 组 DDL 的越界表定义 |
| `80c3f34` | 增加消费者统一注册与登录入口 |
| `d112188` | 补齐 B 组 8 表演示数据和 DDL 静态测试 |
| `94e56f4` | 修复认证合并回归、累计消费与会员资产事务 |
| `42c8986` | 接入真实跨组库存、物流、团长目录、支付与消息服务 |

## 4. 当前验证结果

- `dotnet build FreshColdChain.csproj --no-restore`：0 警告、0 错误。
- `dotnet run --project tests/FreshColdChain.Tests/FreshColdChain.Tests.csproj`：42 个事务/业务场景通过。
  - 下单与结算事务：13 个。
  - 客户营销与认证：13 个。
  - 订单生命周期与退款：13 个。
  - 跨组契约与数据最小化：2 个。
  - 消息中心跨组组合：1 个。
- `pwsh -NoProfile -File tests/verify-groupb-ddl.ps1`：8 张核心表、1 张扩展表、必需列、演示数据和职责边界检查通过。
- `npm audit --audit-level=high`：0 个已知漏洞。
- `npm run build`：Vue/Vite 生产构建通过。
- `git diff --check`：通过。
- `dotnet format --verify-no-changes`：仓库既有 A/C/B 混合文件存在大量历史空白格式差异，当前不是全仓门禁；本轮遵守“只改 B 组”未批量格式化其他组文件。

Oracle 集成测试未执行：仓库未提供隔离测试库或可清理的测试 schema，不能向未知共享库写入订单、积分和优惠券数据。

## 5. 对外契约与剩余事项

- REST API 的请求、响应和演示账号见 `doc/GroupB-api-guide.md`。
- A/B/C 组事务与退款协作规则见 `doc/GroupB-cross-group-contracts.md`。
- A 组库存适配通过 `AttachExternalTransaction` 复用 B 组事务；物流适配调用 A 组 `IColdChainLogisticsService`。
- C 组支付、佣金、团长目录和退款均由 B 组调用现有 Service；B 组不再直接访问 C 组表。
- A 组当前发货实现仍以 `AvailableQty` 校验已预留订单，库存接近售罄时可能误判；该问题属于 A 组代码，B 组不越界修改。
- C 组支付审计日志当前使用独立连接，外层订单回滚时存在日志先提交风险；该问题属于 C 组代码，B 组不越界修改。
- 正式部署前必须通过环境变量或 Secret 提供 Oracle 连接字符串，并在隔离 schema 执行 `groupB_ddl.sql`。
