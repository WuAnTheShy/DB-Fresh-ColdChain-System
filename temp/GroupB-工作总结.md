# B 组工作总结

日期：2026-08-08

工作分支：`dev-groupB`

最终远端基线：`origin/dev-groupB`（总结编写前为 `2a5a9d8`）

## 1. 执行范围

本轮严格限定在 B 组负责的消费者、地址、会员、优惠券、积分、订单及订单明细范围内。B 组 DDL 只创建以下 8 张表：

- `Crm_Customers`
- `Crm_UserAddresses`
- `Crm_MemberLevels`
- `Mkt_Coupons`
- `Mkt_CouponRecords`
- `Crm_PointLogs`
- `Biz_Orders`
- `Biz_OrderDetails`

未修改 A/C 组分支，也未在 B 组 DDL 中创建团长、商品、库存、物流、支付、退款或审计表。对 A/C 组的需求只以接口、DTO、Dummy 和文档冻结契约。

## 2. 各分支完成情况

开始和结束前均执行了 `git fetch --all --prune`，最终远端引用如下：

| 分支 | 最新提交 | 综合判断 |
| --- | --- | --- |
| `origin/main` | `127b694` | 已合入较早的 B 组基线，不包含本轮后续完善 |
| `origin/dev-groupA` | `387339a` | 商品、库存、FEFO、动态定价和物流能力较完整；独立临时工作树构建通过，14 个测试通过 |
| `origin/dev-groupB` | `2a5a9d8` | B 组 8 表业务闭环、Vue 消费者端、30 个自动化场景、DDL 自检和接口文档已完成 |
| `origin/dev-groupC` | `07478f3` | 团长、支付、退款、管理员审核已有实现；独立构建通过但有 20 个可空性警告，无自动化测试，最新提交说明供应商登录仍有问题 |

跨组不能直接合并 Service：A 组当前存在自有订单/状态实现，C 组也有独立模型。应以 B 组冻结的字符串 ID、事务和状态契约编写适配器。

## 3. 本轮提交与结果

| 提交 | 小模块 | 主要结果 |
| --- | --- | --- |
| `9664b55` | 前端依赖安全 | 升级 `nanoid`，高危漏洞归零 |
| `17f9a7a` | 退款闭环 | 未发货退款释放库存，已发货退款不回补；积分扣回幂等并与订单状态同事务 |
| `a8866bd` | 状态契约 | 订单统一为 7 个字符串状态，JSON 拒绝整数枚举 |
| `319bcea` | 跨组标识 | `ProductId`、`SupplierId`、`PromoterId` 统一为字符串 |
| `76b0316` | B 组主键 | 8 表主键/内部外键统一为 `VARCHAR2(36)`，应用生成 32 位 GUID，前后端不再转数字 |
| `80c3f34` | 统一认证 | 新增消费者注册/登录入口，使用密码哈希校验和统一失败信息 |
| `d112188` | 演示数据 | 8 张 B 组表均有可联调种子数据，新增 DDL 职责/类型/覆盖检查脚本 |
| `33f1e40` | 接口文档 | 增加 REST API 类型、请求/响应示例、数据库初始化与跨组调用说明 |
| `77c528f` | 配置安全 | 移除仓库中的固定 Oracle 地址和密码，改用环境变量/Secret |
| `2a5a9d8` | 跨组目录契约 | 冻结 A 组商品目录、可信商品查询及 C 组团长合作范围接口，消费者 JSON 隐藏供应商编号 |

所有小模块均已独立提交并推送到 `origin/dev-groupB`。

## 4. 两个修复建议文件的落实情况

| 建议 | 落实结果 |
| --- | --- |
| 退款区分是否发货 | 已完成，并覆盖幂等、发货前释放、发货后不释放及消费者不匹配回滚测试 |
| 异步 `Task` 与事务原子性 | Repository/Service 均异步；跨组写契约复用发起方 `IDbTransaction`，异常整体回滚 |
| `int` 主键改 `varchar` | B 组 8 表和跨组 ID 均改为字符串，DDL 静态扫描无遗留内部数值 ID |
| 统一登录/注册 | 新增 `/api/auth/customer/register` 与 `/api/auth/customer/login` |
| 订单 7 状态字符串统一 | 已统一为 `PENDING_PAYMENT`、`PAID`、`SHIPPED`、`COMPLETED`、`CANCELLED`、`REFUNDING`、`REFUNDED` |
| 每表测试数据 | `groupB_ddl.sql` 已覆盖 B 组 8 表，并提供真实 PasswordHasher 演示哈希 |
| 接口类型与范例文档 | 已新增 `doc/GroupB-api-guide.md` 并更新跨组契约文档 |
| Models 命名冲突 | 新增跨组 DTO 使用 `GroupA`、`GroupB`、`GroupC` 前缀；既有持久化模型继续使用表前缀 `Crm`/`Mkt`/`Biz` |
| A 组商品接口 | B 组已冻结可售商品分页和可信商品批量查询接口，正式实现由 A 组提供 |
| C 组团长/合作范围接口 | B 组已冻结团长列表、详情、供应商合作范围和批量带货校验接口，正式实现和关系表由 C 组提供 |
| 前端不接触供应商编号 | 消费者商品 DTO 的 `SupplierId` 使用 `JsonIgnore`，并有自动化测试防止泄露 |

## 5. 最终验证

- `git fetch --all --prune`：成功，远端引用已更新。
- `dotnet format ... --verify-no-changes`：应用和测试工程通过。
- `dotnet build FreshColdChain.csproj -c Release --no-restore`：0 警告、0 错误。
- `dotnet run --project tests/FreshColdChain.Tests/FreshColdChain.Tests.csproj -c Release --no-restore`：30/30 通过。
  - 下单事务 4 个。
  - 客户营销与认证 12 个。
  - 订单生命周期与退款 12 个。
  - 跨组契约与数据最小化 2 个。
- `pwsh -NoProfile -File tests/verify-groupb-ddl.ps1`：通过。
- 在 `ClientApp` 执行 `npm audit --audit-level=high`：0 个漏洞。
- 在 `ClientApp` 执行 `npm run build`：1825 个模块转换完成，生产构建通过。
- `dotnet publish FreshColdChain.csproj -c Release --no-restore -o tmp/final-publish`：成功，发布目录包含应用 DLL 和 Vue 静态入口。
- 当前跟踪文件敏感信息扫描：固定 Oracle 地址和密码命中数为 0。
- `git diff --check`：通过。

## 6. 尚需外部条件的事项

1. 仓库没有隔离 Oracle 测试 schema，因此未向未知共享库执行写入型集成测试。部署前应在隔离 schema 执行 `groupB_ddl.sql` 并跑完整业务链。
2. A 组需要实现 `IGroupAProductCatalogService`、`IInventoryService` 和 `ILogisticsService` 适配器。
3. C 组需要实现 `IGroupCPromoterCatalogService`、`ICommissionService`，并在财务退款成功后调用 B 组退款积分接口。
4. 当前统一登录接口只验证身份，不签发 Cookie/JWT；正式部署应接入统一会话层。
5. 连接字符串必须通过 `ConnectionStrings__OracleConnection` 或 Secret 注入。
6. 旧凭据曾存在于 Git 历史，即使当前文件已删除，也必须由数据库管理员立即轮换。

## 7. 主要交付入口

- B 组数据库：`groupB_ddl.sql`
- REST API 文档：`doc/GroupB-api-guide.md`
- 跨组契约：`doc/GroupB-cross-group-contracts.md`
- 当前进度：`doc/GroupB-progress.md`
- 自动化业务场景：`tests/FreshColdChain.Tests`
- DDL 自检：`tests/verify-groupb-ddl.ps1`
